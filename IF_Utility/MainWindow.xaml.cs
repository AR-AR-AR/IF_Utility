using System;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using IFConnect;
using Fds.IFAPI;
using System.Net;
using System.Threading;

namespace IF_Utility
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        IFConnectorClient client = new IFConnectorClient();
        BroadcastReceiver receiver = new BroadcastReceiver();
        private bool pCustomCmdSent = false;

        private APIAircraftState pAircraftState = new APIAircraftState();
        private bool autoFplDirectActive = false;

        public MainWindow()
        {
            InitializeComponent();

            //airplaneStateGrid.DataContext = null;
            //airplaneStateGrid.DataContext = new APIAircraftState();

            //mainTabControl.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void PageLoaded(object sender, RoutedEventArgs e)
        {
            ConnectedCanvas.Visibility = Visibility.Collapsed;
            UnConnectedCanvas.Visibility = Visibility.Visible;
            receiver.DataReceived += receiver_DataReceived;
            receiver.StartListening();
        }



        bool serverInfoReceived = false;

        void receiver_DataReceived(object sender, EventArgs e)
        {
            byte[] data = (byte[])sender;

            var apiServerInfo = Serializer.DeserializeJson<APIServerInfo>(UTF8Encoding.UTF8.GetString(data));

            if (apiServerInfo != null)
            {
                Console.WriteLine("Received Server Info from: {0}:{1}", apiServerInfo.Address, apiServerInfo.Port);
                serverInfoReceived = true;
                receiver.Stop();
                Dispatcher.BeginInvoke((Action)(() =>
                {
                    Connect(IPAddress.Parse(apiServerInfo.Address), apiServerInfo.Port);
                }));
            }
            else
            {
                Console.WriteLine("Invalid Server Info Received");
            }
        }

        private void Connect(IPAddress iPAddress, int port)
        {
            client.Connect(iPAddress.ToString(), port);
            FMSControl.Client = client;
            //connectionStateTextBlock.Text = String.Format("Connected ({0}:{1})", iPAddress, port);
            
            UnConnectedCanvas.Visibility = Visibility.Collapsed;
            ConnectedCanvas.Visibility = Visibility.Visible;

            client.CommandReceived += client_CommandReceived;

            client.SendCommand(new APICall { Command = "InfiniteFlight.GetStatus" });
            client.SendCommand(new APICall { Command = "Live.EnableATCMessageListUpdated" });

            Task.Run(() =>
            {

                while (true)
                {
                    try
                    {
                        client.SendCommand(new APICall { Command = "Airplane.GetState" });

                        Thread.Sleep(200);

                    }
                    catch (Exception ex)
                    {

                    }
                }
            });

            Task.Run(() =>
            {

                while (true)
                {
                    try
                    {
                        client.SendCommand(new APICall { Command = "Live.GetTraffic" });
                        client.SendCommand(new APICall { Command = "Live.ATCFacilities" });

                        Thread.Sleep(5000);

                    }
                    catch (Exception ex)
                    {

                    }
                }
            });
        }


        void client_CommandReceived(object sender, CommandReceivedEventArgs e)
        {
            Dispatcher.BeginInvoke((Action)(() =>
            {
                var type = typeof(IFAPIStatus).Assembly.GetType(e.Response.Type);

                //System.IO.File.AppendAllText("C:\\IfApi\\IfApiCommandResp.txt", e.Response.Type + ": " + e.CommandString + "\n");
                //if (pCustomCmdSent)
                //{
                //    txtResp.AppendText(e.Response.Type + " " + e.CommandString + "\n\n");
                //    txtResp.ScrollToEnd();
                //    pCustomCmdSent = false;
                //}

                if (type == typeof(APIAircraftState))
                {
                    var state = Serializer.DeserializeJson<APIAircraftState>(e.CommandString);
                    //Console.WriteLine(e.CommandString);
                    //airplaneStateGrid.DataContext = null;
                    //airplaneStateGrid.DataContext = state;
                    pAircraftState = state;
                    if (FMSControl.autoFplDirectActive) { FMSControl.updateAutoNav(state); }
                    AircraftStateControl.AircraftState = state;
                    //AttitudeIndicator.updateAttitude(pAircraftState.Pitch, pAircraftState.Bank);
                    
                }
                else if (type == typeof(GetValueResponse))
                {
                    var state = Serializer.DeserializeJson<GetValueResponse>(e.CommandString);

                    Console.WriteLine("{0} -> {1}", state.Parameters[0].Name, state.Parameters[0].Value);
                }
                else if (type == typeof(LiveAirplaneList))
                {
                    var airplaneList = Serializer.DeserializeJson<LiveAirplaneList>(e.CommandString);

                    //airplaneDataGrid.ItemsSource = airplaneList.Airplanes;
                }
                else if (type == typeof(FacilityList))
                {
                    var facilityList = Serializer.DeserializeJson<FacilityList>(e.CommandString);

                    //facilitiesDataGrid.ItemsSource = facilityList.Facilities;
                }
                else if (type == typeof(IFAPIStatus))
                {
                    var status = Serializer.DeserializeJson<IFAPIStatus>(e.CommandString);

                    //versionTextBlock.Text = status.AppVersion;
                    //userNameTextBlock.Text = status.LoggedInUser;
                    //deviceNameTextBlock.Text = status.DeviceName;
                    //displayResolutionTextBlock.Text = string.Format("{0}x{1}", status.DisplayWidth, status.DisplayHeight);
                }
                else if (type == typeof(APIATCMessage))
                {
                    var msg = Serializer.DeserializeJson<APIATCMessage>(e.CommandString);

                    //atcMessagesListBox.Items.Add(msg.Message);

                    client.ExecuteCommand("Live.GetCurrentCOMFrequencies");
                }
                else if (type == typeof(APIFrequencyInfoList))
                {
                    var msg = Serializer.DeserializeJson<APIFrequencyInfoList>(e.CommandString);
                    //frequenciesDataGrid.ItemsSource = msg.Frequencies;
                }
                else if (type == typeof(ATCMessageList))
                {
                    var msg = Serializer.DeserializeJson<ATCMessageList>(e.CommandString);
                    //atcMessagesDataGrid.ItemsSource = msg.ATCMessages;
                }
                else if (type == typeof(APIFlightPlan))
                {
                    var msg = Serializer.DeserializeJson<APIFlightPlan>(e.CommandString);
                    Console.WriteLine("Flight Plan: {0} items", msg.Waypoints.Length);
                    FMSControl.fplReceived(msg);
                    foreach (var item in msg.Waypoints)
                    {
                        Console.WriteLine(" -> {0} {1} - {2}, {3}", item.Name, item.Code, item.Latitude, item.Longitude);
                    }
                }
            }));
        }

        //FPL from FPD received/selected. Copy it to the FMS.
        private void FlightPlanDb_FplUpdated(object sender, EventArgs e)
        {
            FMSControl.CustomFPL.waypoints.Clear();
            foreach (FMS.fplDetails f in FpdControl.FmsFpl)
            {
                FMSControl.CustomFPL.waypoints.Add(f);
            }
        }
    }
}
