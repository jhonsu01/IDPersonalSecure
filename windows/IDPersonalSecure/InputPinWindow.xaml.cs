using System.Windows;
using System.Windows.Input;

namespace IDPersonalSecure;

public partial class InputPinWindow : Window
{
    public string Pin => PinBox.Password;

    public InputPinWindow(string prompt)
    {
        InitializeComponent();
        PromptText.Text = prompt;
        PinBox.Focus();
    }

    private void PinBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Ok_Click(sender, e);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Pin.Length < 4) { MessageBox.Show("El PIN debe tener al menos 4 dígitos."); return; }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
