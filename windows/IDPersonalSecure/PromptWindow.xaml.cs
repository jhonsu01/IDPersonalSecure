using System.Windows;
using System.Windows.Input;

namespace IDPersonalSecure;

public partial class PromptWindow : Window
{
    public string Value => ValueBox.Text.Trim();

    public PromptWindow(string prompt, string initial = "")
    {
        InitializeComponent();
        PromptText.Text = prompt;
        ValueBox.Text = initial;
        Loaded += (_, _) => { ValueBox.Focus(); ValueBox.SelectAll(); };
    }

    private void ValueBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Ok_Click(sender, e);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Value.Length == 0) { MessageBox.Show("Escribe el motivo/trámite."); return; }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
