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
    /// Interaction logic for AircraftStatus.xaml
    /// </summary>
    public partial class AircraftStatus : UserControl
    {
        public AircraftStatus()
        {
            InitializeComponent();
        }

        private Fds.IFAPI.APIAircraftState pAcState;
        public Fds.IFAPI.APIAircraftState AircraftState
        {
            get
            {
                if (pAcState == null) { pAcState = new Fds.IFAPI.APIAircraftState(); }
                return pAcState;
            }
            set
            {
                pAcState = value;
                updateView();
            }
        }

        private void updateView()
        {
            Dictionary<String, object> acStateDict = new Dictionary<String, object>();
            var props = typeof(Fds.IFAPI.APIAircraftState).GetProperties();
            foreach (var prop in props)
            {
                object value = prop.GetValue(pAcState, null); // against prop.Name
                if (prop.Name != "Type")
                {
                    acStateDict.Add(prop.Name, value);
                }
            }
            listView.ItemsSource = acStateDict;
        }
    }
}
