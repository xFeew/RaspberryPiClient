/*
 * Author: Szymon Lach 
 * build & deploy https://www.ryadel.com/en/deploy-net-apps-raspberry-pi/
 * 
 * NOTE: Currently only tested and prepared for windows machines!
 * 
 * Goal is to make it work on windows & linux
 * 
 */


using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace RaspberryPiClient
{
    class Program
    {
        public static IConfigurationRoot _configuration;
        private static HubConnection _hubConnection;
        public static Client client;

        private static OSPlatform platform;

        static async Task Main(string[] args)
        {

            //I need to test that on linux
            //Check os version
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                //Linux platform
                platform = OSPlatform.Linux;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                //Windows platform
                platform = OSPlatform.Windows;
            }


            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string logFileName = "log-.txt";
            FileInfo f = new FileInfo(path);
            string drive = Path.GetPathRoot(f.FullName);

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(Path.Combine(path,logFileName), outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}", rollingInterval: RollingInterval.Day)
                .CreateLogger();
                
            Log.Information("Detected platform: {Platform}", platform);

            Log.Information("Starting");
            Log.Information("Creating configurationBuilder");
            try
            {
                _configuration = new ConfigurationBuilder()
                        .SetBasePath(Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, @"..\..\..\")))
                        .AddJsonFile("appsettings.json", false)
                        .Build();
            }
            catch(Exception e)
            {
                Log.Error(e.ToString());
            }



            Log.Information("Fetching connection string from appsettings.json");
            string hubConnectionString = _configuration.GetConnectionString("Hub");
            Log.Information($"HubConnectionString: {hubConnectionString}");

            Log.Information("Building HubConnection");

            try
            {
                _hubConnection = new HubConnectionBuilder().WithUrl(hubConnectionString, conf =>
                {
                    conf.HttpMessageHandlerFactory = (x) => new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
                        
                    };
  
                }).WithAutomaticReconnect().Build();
                _hubConnection.Closed -= RetryConnection;
                _hubConnection.Closed += RetryConnection;
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }

            //Get Interval from appsettings.json
            int refreshRate = Convert.ToInt32(_configuration.GetSection("Settings")["Interval"]);

            Log.Information("Creating new client instance");

            client = new Client(_configuration, _hubConnection, refreshRate);

            //If disconnected try to connect
            if(client._hubConnection.State == HubConnectionState.Disconnected)
            {
                await client.StartConnectionAsync();
            }


            //stop process from closing
            SpinWait.SpinUntil(() => false);

            // Finally, once just before the application exits...
            Log.Warning("Application Exit");
            Log.CloseAndFlush();
        }

        //If somehow disconnected from host eg. lost network connection or server shutdown
        static async Task RetryConnection(Exception ex)
        {
            await client.StartConnectionAsync();
        }
    }
}
