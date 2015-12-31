using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace IF_Utility
{
    /// <summary>
    /// Interaction logic for FlightPlanDb.xaml
    /// </summary>
    public partial class FlightPlanDb : UserControl
    {
        public event EventHandler FplUpdated;

        private List<FMS.fplDetails> pFmsFpl;
        public List<FMS.fplDetails> FmsFpl {
            get {
                if (pFmsFpl == null) { pFmsFpl = new List<FMS.fplDetails>(); }
                return pFmsFpl;
            }
            set {
                if (pFmsFpl == null) { pFmsFpl = new List<FMS.fplDetails>(); }
                pFmsFpl = value;
                if (this.FplUpdated != null) { this.FplUpdated(this,null); }
            }
        }

        public FlightPlanDb()
        {
            InitializeComponent();
        }

        private List<FlightPlanDatabase.ApiDataTypes.FlightPlanSummary> pFplList;

        private void btnGetFplFromFpd_Click(object sender, RoutedEventArgs e)
        {
            FlightPlanDatabase.FpdApi fd = new FlightPlanDatabase.FpdApi();
            List<FlightPlanDatabase.ApiDataTypes.FlightPlanSummary> fpls = new List<FlightPlanDatabase.ApiDataTypes.FlightPlanSummary>();
            try {
                fpls = fd.searchFlightPlans(txtFromICAO.Text, txtDestICAO.Text);
            }catch(Exception ex)
            {
                String exmsg = ex.Message;
                if (ex.Message.Contains(":")) { exmsg = exmsg.Split(':')[1]; }
                if (ex.Message.Contains(")")) { exmsg = exmsg.Split(')')[1]; }
                lblSearchMsg.Content = "Error: " + exmsg;
                return;
            }
            cbFpls.Items.Clear();
            if (fpls == null || fpls.Count < 1)
            {
                lblSearchMsg.Content = "Suitable FPL could not be found.";
                cbFpls.Visibility = Visibility.Collapsed;
            }
            else
            {
                pFplList = new List<FlightPlanDatabase.ApiDataTypes.FlightPlanSummary>();
                pFplList = fpls;
                foreach (FlightPlanDatabase.ApiDataTypes.FlightPlanSummary f in fpls)
                {
                    cbFpls.Items.Add(f.id + " (" + String.Format("{0:0.00}", f.distance) + "nm - " + f.waypoints.ToString() + "wpts)");
                }
                lblSearchMsg.Content = "FPL(s) found. Select below to load.";
                cbFpls.Visibility = Visibility.Visible;
            }
        }

        private void cbFpls_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbFpls.Items.Count > 0 && cbFpls.SelectedValue != null)
            {
                FlightPlanDatabase.FpdApi fd = new FlightPlanDatabase.FpdApi();
                FlightPlanDatabase.ApiDataTypes.FlightPlanDetails planDetail = fd.getPlan(cbFpls.SelectedValue.ToString().Split(' ').First());
                //FMSControl.CustomFPL.waypoints.Clear();
                List<FMS.fplDetails> fpl = new List<FMS.fplDetails>();

                foreach (FlightPlanDatabase.ApiDataTypes.Node wpt in planDetail.route.nodes)
                {
                    FMS.fplDetails n = new FMS.fplDetails();
                    n.WaypointName = wpt.ident;
                    n.Altitude = wpt.alt;

                    //FMSControl.CustomFPL.waypoints.Add(n);
                    fpl.Add(n);
                }
                FmsFpl = fpl;
            }
        }

        private void txtFromICAO_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = (TextBox)sender;
            tb.Text = string.Empty;
            tb.Foreground = Brushes.Black;
            tb.GotFocus -= txtFromICAO_GotFocus;
        }

        private void txtDestICAO_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = (TextBox)sender;
            tb.Text = string.Empty;
            tb.Foreground = Brushes.Black;
            tb.GotFocus -= txtDestICAO_GotFocus;
        }

        private void txtFromICAO_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = (TextBox)sender;
            if (tb.Text == string.Empty)
            {
                tb.Foreground = Brushes.LightGray;
                tb.GotFocus += txtFromICAO_GotFocus;
                tb.Text = "KLAX";
            }
        }

        private void txtDestICAO_LostFocus(object sender, RoutedEventArgs e)
        {
            TextBox tb = (TextBox)sender;
            if (tb.Text == string.Empty)
            {
                tb.Foreground = Brushes.LightGray;
                tb.GotFocus += txtDestICAO_GotFocus;
                tb.Text = "KSAN";
            }
        }
    }
}
