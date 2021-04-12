using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;
using Microsoft.Win32;
using System.Threading.Tasks;

namespace IFCImportUI
{
    public class Node : INotifyPropertyChanged
    {
        public ObservableCollection<Node> Nodes { get; set; }

        public int Level { get; set; }  // Serves as SP "Level" property used to build the hierarchy of phases and operations
        public int EntityLabel { get; set; }  // An Entity Label (a string number)
        public string GlobalId { get; set; }    // A unique ID to serve as "Code" property in SP
        public string Name { get; set; }    // The name 
        public string TypeName { get; set; }    
        public string CalculatedVolume { get; set; }    // An IFC calculated volume property
        public string Volume { get; set; }  // A "VolPlan" property if specified in IFC
        public string Cost { get; set; }    //  "Cost" property if specified in IFC

        private bool isExpanded;
        public bool IsExpanded {
            get { return isExpanded; }
            set
            {
                if (value != this.isExpanded)
                {
                    this.isExpanded = value;
                    NotifyPropertyChanged("IsExpanded");
                }
            }
        }

        private bool isChecked;
        public bool IsChecked
        {
            get { return isChecked; }
            set
            {
                if (value != this.IsChecked)
                {
                    this.isChecked = value;
                    NotifyPropertyChanged("IsChecked");
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propName)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, new PropertyChangedEventArgs(propName));
        }
    }

    public partial class MainWindow : Window
    {
        ObservableCollection<Node> _nodes;
        string _ifcPath;
        bool _volumePropertyNameChanged = false;
        bool _costPropertyNameChanged = false;
        bool _materialPropertyNameChanged = false;

        public MainWindow()
        {
            InitializeComponent();
            setIfcStatusControls(ExportStatus.chooseIfc);
            tvIFC.Height = System.Windows.SystemParameters.PrimaryScreenHeight - 200;  // 500;
        }

        private void tbVolumePropertyName_TextChanged(object sender, RoutedEventArgs e)
        {
            _volumePropertyNameChanged = true;
            setIfcStatusControls(ExportStatus.notParsed);
        }
        private void tbCostPropertyName_TextChanged(object sender, RoutedEventArgs e)
        {
            _costPropertyNameChanged = true;
            setIfcStatusControls(ExportStatus.notParsed);
        }
        private void tbMaterialPropertyName_TextChanged(object sender, RoutedEventArgs e) {
            _materialPropertyNameChanged = true;
            setIfcStatusControls(ExportStatus.notParsed);
        }

        private void clearNodes()
        {
            tvIFC.ItemsSource = null;
            cmbLevels.Items.Clear();
            _nodes = new ObservableCollection<Node>();
        }

        private void cmbLevels_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBox cmb = sender as ComboBox;
            expandToLevel(cmb.SelectedIndex + 1, _nodes);
        }

        private void tvIFC_Expanded(object sender, RoutedEventArgs e)
        {
            if (tvIFC.SelectedItem != null)
                (tvIFC.SelectedItem as Node).IsExpanded = !(tvIFC.SelectedItem as Node).IsExpanded;
        }

        private void expandToLevel( int maxLevel, ObservableCollection<Node> nodes)
        {
            foreach(var node in nodes)
            {
                if (node.Level < maxLevel) {
                    node.IsExpanded = true;
                }
                else {
                    node.IsExpanded = false;
                }
                if (node.Nodes != null) {
                    expandToLevel(maxLevel, node.Nodes);
                }
            }
        }

        private void btnChooseIfcFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == true )
            {
                clearNodes();
                tbIfcPath.Text = openFileDialog.FileName;
                tbIfcPath.Height = System.Double.NaN;// GridLength.Auto;
                _ifcPath = openFileDialog.FileName;
                setIfcStatusControls(ExportStatus.notParsed);
            }
            else
            {
                tbIfcPath.Text = null;
                tbIfcPath.Height = 0;
                _ifcPath = null;
                setIfcStatusControls(ExportStatus.chooseIfc);
            }
        }

        private async void btnParseIfcFile_Click(object sender, RoutedEventArgs e)
        {
            clearNodes();
            setIfcStatusControls(ExportStatus.waitParsing);

            string vname = tbVolumePropertyName.Text;
            string cname = tbCostPropertyName.Text;
            string mname = tbMaterialPropertyName.Text;
            int maxLevel = await Task.Run(() => {
                int level = 1;
                _ifc = new Ifc(_ifcPath, ref _nodes, ref level, vname, cname, mname);
                return level;
            });
            if (_ifc.isParsedOk())
            {
                tvIFC.ItemsSource = _nodes;
                expandToLevel(1, _nodes);

                for (int i = 1; i <= maxLevel; i++)
                    cmbLevels.Items.Add(new ComboBoxItem { Content = i.ToString() });
                cmbLevels.SelectedIndex = 0;
                _volumePropertyNameChanged = false;
                _costPropertyNameChanged = false;
                _materialPropertyNameChanged = false;
                setIfcStatusControls(ExportStatus.Parsed);
            }
            else
            {
                setIfcStatusControls(ExportStatus.failedToParse);
            }
        }

        private void btnWexbimPath_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.CheckFileExists = false;
            openFileDialog.Filter = "Wexbim Files(*.wexbim)|*.wexbim|All Files(*.*)|*.*";
            if (openFileDialog.ShowDialog() == true)
            {
                tbWexbimPath.Text = openFileDialog.FileName;
                tbWexbimPath.Height = System.Double.NaN;
            } else
            {
                tbWexbimPath.Text = null;
                tbWexbimPath.Height = 0;
            }
        }

        private async void btnExport_Click(object sender, RoutedEventArgs e)
        {
            if( !_ifc.isParsedOk() )
                return;
            setIfcStatusControls(ExportStatus.waitExporting);

            string server = tbServer.Text;
            string wexbimPath = tbWexbimPath.Text;
            int status = await Task.Run( () => { return Export(ref server, ref wexbimPath); } );
            if (status == 0)
                setIfcStatusControls(ExportStatus.exported);
            else
                setIfcStatusControls(ExportStatus.failedToExport);
        }

        private int Export( ref string server, ref string wexbimPath )
        {
            if (wexbimPath != null && !wexbimPath.Equals(""))    // If there is a wexbim path specified
            {
                int wexbimStatus = _ifc.createWexbim(wexbimPath);
            }
            int n = 0;
            string activities = "[";
            formatActivities(ref _nodes, ref activities, 0, ref n);
            activities += "]";
            string materials;
            formatMaterials(out materials);
            string materialAssignments;
            formatMaterialAssignments(out materialAssignments);
            int status = Api.Run( ref server, ref activities, ref materials, ref materialAssignments, 
                ref _ifcPath, ref wexbimPath);
            return status;
        }

        private void formatMaterials( out string dest )
        {
            dest = "";
            if(_ifc._materials != null && _ifc._materials.Count > 0 )
            {
                bool first = true;
                foreach (var pair in _ifc._materials) {
                    if (first) {
                        dest += "[";
                        first = false;
                    } else {
                        dest += ",";
                    }
                    dest += "{\"Code\":\"" + pair.Key + "\", \"Name\":\"" + pair.Value + "\"}";
                }
            }
            if (dest.Length > 0)
                dest += "]";
        }

        private void formatMaterialAssignments( out string dest )
        {
            dest = "";
            if( _ifc._materialAssignments.Count > 0 )
            {
                bool first = true;
                foreach( var it in _ifc._materialAssignments )
                {
                    if( first) {
                        dest += "[";
                        first = false;
                    } else {
                        dest += ",";
                    }
                    dest += "{\"OperCode\":\"" + it._acode + 
                        "\", \"MatCode\":\"" + it._mcode + "\", \"Fix\":\"" + it._volume + "\"}";
                }
            }
            if (dest.Length > 0)
                dest += "]";
        }

        private void formatActivities( ref ObservableCollection<Node> nodes, ref string dest, int skippedLevels, ref int n )
        {
            int skipLevel;

            foreach( var node in nodes )
            {
                if( node.IsChecked ) {
                    if (n > 0)
                        dest += ",";                                                      
                    dest += "{";
                    dest += "\"Level\":" + (node.Level - skippedLevels).ToString();
                    dest += ", \"Code\":\"" + node.GlobalId + "\"";
                    dest += ", \"Name\":\"" + node.Name + "\"";
                    if( node.CalculatedVolume.Length > 0 )
                        dest += ", \"CalculatedVolume\":" + node.CalculatedVolume;
                    if( node.Volume.Length > 0 )
                        dest += ", \"VolPlan\":" + node.Volume;
                    if( node.Cost.Length > 0 )
                        dest += ", \"Cost\":" + node.Cost;
                    dest += ", \"f_Model\":\"" + node.EntityLabel + "\"";
                    dest += "}";
                    n++;
                    skipLevel = 0;
                }
                else {
                    skipLevel = 1;
                }

                if (!node.IsExpanded)
                    continue;
                if (node.Nodes == null)
                    continue;
                if (node.Nodes.Count == 0)
                    continue;
                ObservableCollection<Node> childNodes = node.Nodes;
                formatActivities(ref childNodes, ref dest, skippedLevels + skipLevel, ref n);
            }
        }
    }
}
