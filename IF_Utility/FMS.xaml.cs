using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Fds.IFAPI;
using System.ComponentModel;
using System.Collections.Specialized;

namespace IF_Utility
{
    /// <summary>
    /// Interaction logic for FMS.xaml
    /// </summary>
    public partial class FMS : UserControl
    {
        private IFConnect.IFConnectorClient client;
        public IFConnect.IFConnectorClient Client {
            get { return client; }
            set { client = value; }
        }

        private APIAircraftState pAircraftState = new APIAircraftState();
        private bool autoFplDirectActive = false;

        public FMS()
        {
            InitializeComponent();
        }

        public class fplDetails : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            private string pWpt;
            public string WaypointName {
                get { return pWpt; }
                set
                {
                    pWpt = value;
                    this.NotifyPropertyChanged("WaypointName");
                }
            }

            private double pAlt;
            public double Altitude {
                get { return pAlt; }
                set
                {
                    pAlt = value;
                    this.NotifyPropertyChanged("Altitude");
                }
            }

            private double pSpeed;
            public double Airspeed
            {
                get { return pSpeed; }
                set
                {
                    pSpeed = value;
                    this.NotifyPropertyChanged("Airspeed");
                }
            }

            private void NotifyPropertyChanged(string name)
            {
                if (PropertyChanged != null)
                    PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        private bool pManualRetrieveFPL = false;

        private void WaypointsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            //different kind of changes that may have occurred in collection
            if (false && CustomFplWaypoints.Count > 0 && !pManualRetrieveFPL)
            {
                if (e.Action == NotifyCollectionChangedAction.Add)
                {
                    setCustomFlightPlan();
                }
                if (e.Action == NotifyCollectionChangedAction.Replace)
                {
                    setCustomFlightPlan();
                }
                if (e.Action == NotifyCollectionChangedAction.Remove)
                {
                    setCustomFlightPlan();
                }
                if (e.Action == NotifyCollectionChangedAction.Move)
                {
                    setCustomFlightPlan();
                }
            }
        }

        public class customFlightplan
        {
            private System.Collections.ObjectModel.ObservableCollection<fplDetails> pWaypoints;
            public System.Collections.ObjectModel.ObservableCollection<fplDetails> waypoints {
                get {
                    if (pWaypoints == null)
                    {
                        pWaypoints = new System.Collections.ObjectModel.ObservableCollection<fplDetails>();
                    }
                    return pWaypoints;
                 }
                
            }

            public void addWaypoint(String wpt, double altitude, double airspeed)
            {
                //if (waypoints == null) { waypoints = new System.Collections.ObjectModel.ObservableCollection<fplDetails>(); }
                waypoints.Add(new fplDetails { WaypointName = wpt, Altitude = altitude, Airspeed = airspeed });
            }
        }

        private customFlightplan pCustomFpl;
        public customFlightplan CustomFPL
        {
            get {
                if (pCustomFpl == null)
                {
                    pCustomFpl = new customFlightplan();
                    pCustomFpl.waypoints.CollectionChanged += new NotifyCollectionChangedEventHandler(WaypointsChanged);
                }
                return pCustomFpl;
            }
            set { pCustomFpl = value; }
        }

        public System.Collections.ObjectModel.ObservableCollection<fplDetails> CustomFplWaypoints
        {
            get { return CustomFPL.waypoints; }
        }

        public void setCustomFlightPlan()
        {
            var items = CustomFPL.waypoints;
            client.ExecuteCommand("Commands.FlightPlan.Clear");
            client.ExecuteCommand("Commands.FlightPlan.AddWaypoints", items.Select(x => new CallParameter { Name = "WPT", Value = x.WaypointName }).ToArray());
        }

        private void btnGetFpl_Click(object sender, RoutedEventArgs e)
        {
            client.ExecuteCommand("FlightPlan.GetFlightPlan");
        }

        private void btnSetFpl_Click(object sender, RoutedEventArgs e)
        {
            setCustomFlightPlan();
        }

        private void btnClrFpl_Click(object sender, RoutedEventArgs e)
        {
            CustomFPL.waypoints.Clear();
            client.ExecuteCommand("Commands.FlightPlan.Clear");
        }

        public void fplReceived(APIFlightPlan fpl)
        {
            pManualRetrieveFPL = true;
            CustomFPL.waypoints.Clear(); //= new customFlightplan();

            foreach (APIWaypoint wpt in fpl.Waypoints)
            {
                if (wpt.Name != "WPT") {
                    CustomFPL.addWaypoint(wpt.Name, -1, -1);
                }
            }

 //          this.dgFpl.ItemsSource = CustomFPL.waypoints;
            dgFpl.Items.Refresh();
            pFplState.fplDetails = CustomFPL;
            pManualRetrieveFPL = false;
        }


        private void dgFplEdited(object sender, DataGridCellEditEndingEventArgs e)
        {
  //          setCustomFlightPlan();
            //customFlightplan tempFpl = new customFlightplan();
            //foreach (var item in dgFpl.Items)
            //{
            //    if (item is fplDetails)
            //    {
            //        if (((fplDetails)item).WaypointName != "WPT" && ((fplDetails)item).WaypointName != null) { tempFpl.addWaypoint(((fplDetails)item).WaypointName, ((fplDetails)item).Altitude, ((fplDetails)item).Airspeed); }
            //    }
            //    else
            //    {
            //        Console.Write("");
            //    }
            //}

            //CustomFPL = tempFpl;

            //pFplState.fplDetails = CustomFPL;
        }


        public class flightPlanState
        {
            public APIFlightPlan fpl; //Entire FPL
            public customFlightplan fplDetails;
            public int legIndex; //Index of leg (really wpt index)
            public APIWaypoint prevWpt; //Last waypoint
            public APIWaypoint nextWpt; //Next waypoint
            public APIWaypoint dest; //Destination
            public double distToNextWpt;
            public double hdgToNextWpt;
            public double distToDest;
            public double nextAltitude;
            public double thisSpeed;
            public event EventHandler fplStateUpdate;
        }

        private flightPlanState pFplState;
        public flightPlanState FPLState
        {
            get { return pFplState; }
            set { pFplState = value; }
        }

        private void autoFplDirect(APIAircraftState acState)
        {
            if (pFplState == null)
            {
                client.ExecuteCommand("FlightPlan.GetFlightPlan");
            }

            //Set initial next waypoint as first waypoint
            if (pFplState.nextWpt == null)
            {
                pFplState.legIndex = 0;
                pFplState.nextWpt = pFplState.fpl.Waypoints[1];
                pFplState.dest = pFplState.fpl.Waypoints.Last();
            }

            //Get dist to next wpt
            pFplState.distToNextWpt = getDistToWaypoint(acState.Location, pFplState.nextWpt);

            //If dist<2nm, go to next wpt
            if (pFplState.distToNextWpt < 2)
            {
                pFplState.legIndex++; //next leg
                pFplState.prevWpt = pFplState.nextWpt; //current wpt is not prev wpt
                pFplState.nextWpt = pFplState.fpl.Waypoints[pFplState.legIndex]; //target next wpt
                pFplState.nextAltitude = pFplState.fplDetails.waypoints[pFplState.legIndex].Altitude;
                pFplState.thisSpeed = pFplState.fplDetails.waypoints[pFplState.legIndex - 1].Airspeed;
                //Get dist to new next wpt
                pFplState.distToNextWpt = getDistToWaypoint(acState.Location, pFplState.nextWpt);
            }

            lblNextWpt.Content = pFplState.nextWpt.Name;
            lblDist2Next.Content = pFplState.distToNextWpt.ToString();

            double declination = acState.HeadingTrue - acState.HeadingMagnetic;

            //Get heading to next
            pFplState.hdgToNextWpt = getHeadingToWaypoint(acState.Location, pFplState.nextWpt) - declination;
            lblHdg2Next.Content = pFplState.hdgToNextWpt.ToString();

            //Adjust AutoPilot
            setAutoPilotParams(pFplState.nextAltitude, pFplState.hdgToNextWpt, 0, pFplState.thisSpeed);


            ////Dont think we need this
            //APIWaypoint closestWpt = pFplState.fpl.Waypoints.First();
            //foreach (APIWaypoint  wpt in pFplState.fpl.Waypoints)
            //{
            //   double distToClosest = getDistToWaypoint(acState.Location, closestWpt);
            //   if (getDistToWaypoint(acState.Location,wpt) < distToClosest)
            //    {
            //        closestWpt = wpt;
            //    }
            //}


        }

        private void setAutoPilotParams(double altitude, double heading, double vs, double speed)
        {
            //Send parameters
            if (speed > 0) { client.ExecuteCommand("Commands.Autopilot.SetSpeed", new CallParameter[] { new CallParameter { Value = speed.ToString() } }); }
            if (altitude > 0) { client.ExecuteCommand("Commands.Autopilot.SetAltitude", new CallParameter[] { new CallParameter { Value = altitude.ToString() } }); }
            //client.ExecuteCommand("Commands.Autopilot.SetVS", new CallParameter[] { new CallParameter { Value = vs.ToString() } });
            client.ExecuteCommand("Commands.Autopilot.SetHeading", new CallParameter[] { new CallParameter { Value = heading.ToString() } });
            //Activate AP
            if (altitude > 0) { client.ExecuteCommand("Commands.Autopilot.SetAltitudeState", new CallParameter[] { new CallParameter { Value = "True" } }); }
            client.ExecuteCommand("Commands.Autopilot.SetHeadingState", new CallParameter[] { new CallParameter { Value = "True" } });
            //client.ExecuteCommand("Commands.Autopilot.SetVSState", new CallParameter[] { new CallParameter { Value = "True" } });
            if (speed > 0) { client.ExecuteCommand("Commands.Autopilot.SetSpeedState", new CallParameter[] { new CallParameter { Value = "True" } }); }
            //if (appr) { client.ExecuteCommand("Commands.Autopilot.SetApproachModeState", new CallParameter[] { new CallParameter { Value = appr.ToString() } }); }
        }

        private double deg2rad(double deg)
        {
            return deg * (Math.PI / 180);
        }

        private double rad2deg(double rad)
        {
            return rad * (180 / Math.PI);
        }

        private double getDistToWaypoint(Coordinate curPos, APIWaypoint nextWpt)
        {
            var R = 3440; // Radius of the earth in nm
            var dLat = deg2rad(nextWpt.Latitude - curPos.Latitude);  // deg2rad below
            var dLon = deg2rad(nextWpt.Longitude - curPos.Longitude);
            var a =
              Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
              Math.Cos(deg2rad(curPos.Latitude)) * Math.Cos(deg2rad(nextWpt.Latitude)) *
              Math.Sin(dLon / 2) * Math.Sin(dLon / 2)
              ;
            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            var d = R * c; // Distance in nm
            return d;
        }

        private double getHeadingToWaypoint(Coordinate curPos, APIWaypoint nextWpt)
        {
            double longitude1 = curPos.Longitude;
            double longitude2 = nextWpt.Longitude;
            double latitude1 = deg2rad(curPos.Latitude);
            double latitude2 = deg2rad(nextWpt.Latitude);
            double longDiff = deg2rad(longitude2 - longitude1);
            double y = Math.Sin(longDiff) * Math.Cos(latitude2);
            double x = Math.Cos(latitude1) * Math.Sin(latitude2) - Math.Sin(latitude1) * Math.Cos(latitude2) * Math.Cos(longDiff);

            return (rad2deg(Math.Atan2(y, x)) + 360) % 360;
        }

        private void btnInitFlightDir_Click(object sender, RoutedEventArgs e)
        {

            if (pFplState.fpl == null)
            {
                MessageBox.Show("Must get or set FPL first.");
            }
            else if (pFplState.fpl.Waypoints.Length > 0)
            {
                autoFplDirect(pAircraftState);
                autoFplDirectActive = true;
            }
        }

        private void btnDisFlightDir_Click(object sender, RoutedEventArgs e)
        {
            autoFplDirectActive = false;
            pFplState = null;
        }


    }
}
