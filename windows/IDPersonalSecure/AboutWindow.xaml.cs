using System.Diagnostics;
using System.Reflection;
using System.Windows;

namespace IDPersonalSecure;

public partial class AboutWindow : Window
{
    private const string RepoUrl = "https://github.com/jhonsu01/IDPersonalSecure";
    private const string KofiUrl = "https://ko-fi.com/V7V81LV7GX";

    public AboutWindow()
    {
        InitializeComponent();
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = v != null ? $"v{v.Major}.{v.Minor}.{v.Build}" : "";
    }

    private void Repo_Click(object sender, RoutedEventArgs e) => Open(RepoUrl);
    private void Kofi_Click(object sender, RoutedEventArgs e) => Open(KofiUrl);
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }
}
