//Author: Szymon Lach 
//Company: Hutchinson
//build & deploy https://www.ryadel.com/en/deploy-net-apps-raspberry-pi/


using System;
using System.Threading;
using Iot.Device;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http;
using System.Net;
using System.Collections;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Sinks.SystemConsole;
using Serilog.Sinks.File;
using System.IO;
using System.Threading.Tasks;

namespace RaspberryPiClient
{
    class Program
    {
        public static IConfigurationRoot _configuration;
        private static HubConnection _hubConnection;

        static async Task Main(string[] args)
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string logFileName = "log-.txt";
            FileInfo f = new FileInfo(path);
            string drive = Path.GetPathRoot(f.FullName);

            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(Path.Combine(path,logFileName), outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            Log.Information("Starting.");
            Log.Information("Creating configurationBuilder.");
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
            }
            catch (Exception e)
            {
                Log.Error(e.ToString());
            }


            Log.Information("Creating Client instace");

            //Get Interval from appsettings.json
            int refreshRate = Convert.ToInt32(_configuration.GetSection("Settings")["Interval"]);

            Client client = new Client(_configuration, _hubConnection, refreshRate);
            await client.Connect();



            //stop process from closing
            SpinWait.SpinUntil(() => false);

            // Finally, once just before the application exits...
            Log.Warning($"Application Exit");
            Log.CloseAndFlush();
        }
    }
}
