using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using Serilog;
using System.Net.Http;
using System.Collections.Generic;

namespace RaspberryPiClient
{
    public class Client
    {
        private bool Handshake = false;
        
        public HubConnection _hubConnection;
        public IConfiguration _configuration;

        public string _connectionId;
        public string _hostName;
        public string _ipv4Address;
        public string _macAddress;
        public string _deviceName;
        public int _refreshRate;


        public Client(IConfiguration configuration, HubConnection hubConnection, int refreshRate)
        {
            this._configuration = configuration;
            this._hubConnection = hubConnection;
            this._refreshRate = refreshRate;
            this._hostName = SetHostName();
            this._deviceName = SetDeviceName();
            this._macAddress = SetMacAddress();
            this._ipv4Address = SetIpAddress();
            this._hubConnection.Closed -= OnConnectionClosedAsync;
            this._hubConnection.Closed += OnConnectionClosedAsync;
            this._hubConnection.Reconnected -= Connect;
            this._hubConnection.Reconnected += Connect;

            Log.Information("Client HostName: {HostName}", this._hostName);
            Log.Information("Client DeviceName: {DeviceName}", this._deviceName);
            Log.Information("Client IPv4 Address: {Ip}", this._ipv4Address);
            Log.Information("Client Mac Address: {Mac}", this._macAddress);
            Log.Information("Refresh Rate: {RefreshRate}", this._refreshRate);
        }

        public async Task Connect(string connectionId)
        {
            this.Handshake = false;
            Log.Information("Established connection to {ConnectedTo}", this._configuration.GetConnectionString("Hub"));
            this._connectionId = connectionId;
            //listen
            try
            {
                _hubConnection.On<string>("ReceivedMessage", (message) =>
                {
                    //Todo make this better
                    if (message.Contains(_connectionId) && !this.Handshake)
                    {
                        Log.Information("Welcome message from host: {Message}",message);
                        this.Handshake = true;
                        Log.Information("Handshake");
                        Checker();
                    }
                    else
                    {
                        Log.Information("Received data from host: {Message}",message);
                    }
                });

                _hubConnection.On<string>("Status", (message) =>
                {
                    if (message == "OK")
                    {
                        Log.Information("Status is OK");
                    }
                    else
                    {
                        Log.Fatal("Something is not ok");
                        //abort?
                        throw new Exception("FATAL ERROR");
                    }
                });


                SaveClientSettings();

                Log.Information("Subscribing to {Hub}",_configuration.GetConnectionString("Hub"));
                //Serialize class variables as json and send them to Hub
                await _hubConnection.SendAsync("Subscribe", JsonConvert.SerializeObject(this));
            }
            catch (HttpRequestException e)
            {
                Log.Error(e.ToString());
            }
        }

        public async Task StartConnectionAsync()
        {
            try
            {

                //Try to start
                if (this._hubConnection.State == HubConnectionState.Disconnected)
                {
                    await this._hubConnection.StartAsync();     
                }

                Log.Information("Client state: {State}",this._hubConnection.State);

                //Listen when connected
                if(this._hubConnection.State == HubConnectionState.Connected)
                {
                    await Connect(this._connectionId);
                }
            }
            catch (Exception ex)
            {
                Log.Information("Client state: {State}",this._hubConnection.State);
                await OnConnectionExceptionAsync(ex);
            }
        }

        private async Task OnConnectionExceptionAsync(Exception exception)
        {
            await OnConnectionClosedAsync(exception);
        }
        private async Task OnConnectionClosedAsync(Exception ex)
        {
            await Task.Delay(2000);
            await StartConnectionAsync();
        }


        //Task executing each X ms 
        //if interval is set 0 it won't start
        #region Checker
        public void Checker()
        {
            if(this._refreshRate >0)
            {
                var autoEvent = new AutoResetEvent(false);
                var stateTimer = new Timer(CheckStatus, autoEvent, this._refreshRate, 250);

                stateTimer.Change(0, this._refreshRate);
            }
        }

        public async void CheckStatus(Object stateInfo)
        {
            AutoResetEvent autoEvent = (AutoResetEvent)stateInfo;
            Log.Information("{0} Checking status...", DateTime.Now.ToString("h:mm:ss.fff"));
            if(this._hubConnection.State == HubConnectionState.Disconnected)
            {
                Log.Information("Disconnected");

            }

           // await _hubConnection.SendAsync("UpdateStatus", _connectionId);
        }
        #endregion

        //Access methods (Get,Set)
        #region Access Methods
        public string SetHostName()
        {
            return Dns.GetHostName();
        } 
        
        public string SetDeviceName()
        {
            return Environment.MachineName;
        }

        public string SetIpAddress()
        {
            IPHostEntry ipEntry = Dns.GetHostEntry(this._hostName);
            IPAddress[] addr = ipEntry.AddressList;
            Regex reg = new Regex(@"^((25[0-5]|(2[0-4]|1\d|[1-9]|)\d)(\.(?!$)|$)){4}$");

            for (int i = 0; i < addr.Length; i++)
            {
                Log.Information("IP Address {0}: {1} ", i, addr[i].ToString());
                if (reg.Match(addr[i].ToString()).Success)
                {
                    return addr[i].ToString();
                }
            }
            return string.Empty;
        }

        public string SetMacAddress()
        {
            var macAddr =
            (
                from nic in NetworkInterface.GetAllNetworkInterfaces()
                where nic.OperationalStatus == OperationalStatus.Up
                select nic.GetPhysicalAddress().ToString()
            ).FirstOrDefault();

            return macAddr;
        }
        #endregion


        //Misc methods
        public void SaveClientSettings()
        {
            //Update connectionId
            this._connectionId = _hubConnection.ConnectionId;
            //Deserialize class instance and its values into json file
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string json = JsonConvert.SerializeObject(this);

            //Save them in ClientSettings-DATE.json
            File.WriteAllText(Path.Combine(path,$"ClientSettings-{DateTime.Now.Year}{DateTime.Now.Month}{DateTime.Now.Day}.json"), json);
           // Log.Information(json);
        }
    }
}