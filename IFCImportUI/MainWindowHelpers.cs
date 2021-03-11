using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.ComponentModel;

namespace IFCImportUI
{
    public partial class MainWindow : Window
    {
        enum ExportStatus
        {
            auto, chooseIfc, notParsed, waitParsing, failedToParse, Parsed,
            exported, waitExporting, failedToExport
        };

        Ifc _ifc;
        string _msgChooseIfc = "Please choose an IFC an...";
        string _msgNotParsed = "Please parse the IFC file...";
        string _msgwaitParsing = "Please wait while parsing the IFC file...";
        string _msgfailedToParse = "Failed to parse the IFC file...";
        string _msgParsed = "You may now export the data into Spider Project";
        string _msgExported = "IFC data has been successfully exported!";
        string _msgWaitExporting = "Please wait while exporting the IFC file...";
        string _msgFailedToExport = "Error exporting the IFC file...";

        System.Windows.Media.SolidColorBrush _disableColor = new System.Windows.Media.SolidColorBrush(SystemColors.GrayTextColor);
        System.Windows.Media.SolidColorBrush _activeColor = new System.Windows.Media.SolidColorBrush(SystemColors.ControlTextColor);
        private void setIfcStatusControls(ExportStatus status)
        {
            if (status == ExportStatus.auto && _nodes == null)
                tbPrompt.Text = _msgNotParsed;
            else if (status == ExportStatus.auto && _nodes != null)
                tbPrompt.Text = "";
            else if (status == ExportStatus.chooseIfc)
                tbPrompt.Text = _msgChooseIfc;
            else if (status == ExportStatus.waitParsing)
                tbPrompt.Text = _msgwaitParsing;
            else if (status == ExportStatus.Parsed)
                tbPrompt.Text = _msgParsed;
            else if (status == ExportStatus.notParsed)
                tbPrompt.Text = _msgNotParsed;
            else if (status == ExportStatus.failedToParse)
                tbPrompt.Text = _msgfailedToParse;
            else if (status == ExportStatus.waitExporting)
                tbPrompt.Text = _msgWaitExporting;
            else if (status == ExportStatus.exported)
                tbPrompt.Text = _msgExported;
            else if (status == ExportStatus.failedToExport)
                tbPrompt.Text = _msgFailedToExport;
            if (_nodes != null && _nodes.Count > 0 && status != ExportStatus.waitExporting)
            {
                cmbLevels.IsEnabled = true;
                btnWexbimPath.IsEnabled = true;
                btnExport.IsEnabled = true;
                cmbLevelsTitle.Foreground = _activeColor;
            }
            else
            {
                cmbLevels.IsEnabled = false;
                btnWexbimPath.IsEnabled = false;
                btnExport.IsEnabled = false;
                cmbLevelsTitle.Foreground = _disableColor;
            }
            if (status != ExportStatus.waitParsing && status != ExportStatus.waitExporting &&
                tbIfcPath.Text != null && !tbIfcPath.Text.Equals(""))
            {
                tbVolumePropertyName.IsEnabled = true;
                tbCostPropertyName.IsEnabled = true;
                tbVolumePropertyNameTitle.Foreground = _activeColor;
                tbCostPropertyNameTitle.Foreground = _activeColor;
            }
            else
            {
                tbVolumePropertyName.IsEnabled = false;
                tbCostPropertyName.IsEnabled = false;
                tbVolumePropertyNameTitle.Foreground = _disableColor;
                tbCostPropertyNameTitle.Foreground = _disableColor;
            }
            bool changed = _volumePropertyNameChanged || _costPropertyNameChanged;
            btnParseIfcFile.IsEnabled =
                ((status == ExportStatus.notParsed) ||
                (status != ExportStatus.waitParsing && status != ExportStatus.waitExporting && changed)) ? true : false;
            btnChooseIfcFile.IsEnabled =
                (status == ExportStatus.waitExporting || status == ExportStatus.waitParsing) ? false : true;
            btnWexbimPath.IsEnabled =
                (status == ExportStatus.waitExporting || status == ExportStatus.waitParsing) ? false : true;
            btnExport.IsEnabled =
                (status == ExportStatus.waitExporting || status == ExportStatus.waitParsing) ? false : true;
        }
    }
}
