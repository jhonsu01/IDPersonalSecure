using System.Windows;

namespace IDPersonalSecure;

public partial class ShareOptionsWindow : Window
{
    public bool ApplyWatermark => WatermarkCheck.IsChecked == true;
    public string Tramite => TramiteBox.Text.Trim();
    public string Recipient => RecipientBox.Text.Trim();

    public ShareOptionsWindow()
    {
        InitializeComponent();
        UpdateTramiteState();
    }

    private void Watermark_Changed(object sender, RoutedEventArgs e) => UpdateTramiteState();

    private void UpdateTramiteState()
    {
        bool on = WatermarkCheck.IsChecked == true;
        TramiteLabel.Opacity = on ? 1.0 : 0.4;
        TramiteBox.IsEnabled = on;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (ApplyWatermark && Tramite.Length == 0)
        {
            MessageBox.Show("Escribe el motivo/trámite para la marca de agua, o desactívala.");
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
