using System.Windows;
using IDPersonalSecure.Data;

namespace IDPersonalSecure;

public partial class HistoryWindow : Window
{
    private readonly VaultRepository _repo;

    public HistoryWindow(VaultRepository repo)
    {
        InitializeComponent();
        _repo = repo;
        Refresh();
    }

    private void Refresh()
    {
        LogList.ItemsSource = null;
        LogList.ItemsSource = _repo.ShareLog;
        EmptyText.Visibility = _repo.ShareLog.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void EditRecipient_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ShareRecord rec) return;
        var prompt = new PromptWindow("¿A quién le compartiste esta copia?", rec.Recipient) { Owner = this, Title = "Destinatario" };
        // PromptWindow exige texto no vacío; para permitir limpiar, aceptamos lo que devuelva.
        if (prompt.ShowDialog() == true)
        {
            _repo.UpdateShareRecipient(rec.Id, prompt.Value);
            Refresh();
        }
    }
}
