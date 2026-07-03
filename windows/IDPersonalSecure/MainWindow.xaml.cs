using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using IDPersonalSecure.Data;
using Microsoft.Win32;

namespace IDPersonalSecure;

public partial class MainWindow : Window
{
    private readonly VaultRepository _repo = new();
    private ICollectionView? _view;

    public MainWindow()
    {
        InitializeComponent();
        FilterBox.ItemsSource = new[] { "Todos", "Vencidos", "Próximos", "Sin vencimiento" };
        FilterBox.SelectedIndex = 0;
        LoginSubtitle.Text = _repo.VaultExists() ? "Ingresa tu PIN" : "Crea un PIN para tu nueva bóveda";
        UnlockButton.Content = _repo.VaultExists() ? "Desbloquear" : "Crear bóveda";
        PinBox.Focus();
    }

    private void PinBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Unlock_Click(sender, e);
    }

    private void Unlock_Click(object sender, RoutedEventArgs e)
    {
        string pin = PinBox.Password;
        if (pin.Length < 4) { LoginError.Text = "El PIN debe tener al menos 4 dígitos"; return; }
        if (!_repo.Unlock(pin)) { LoginError.Text = "PIN incorrecto o bóveda corrupta"; return; }

        LoginError.Text = "";
        _view = CollectionViewSource.GetDefaultView(_repo.Documents);
        _view.Filter = FilterPredicate;
        DocList.ItemsSource = _view;
        LoginPanel.Visibility = Visibility.Collapsed;
        VaultPanel.Visibility = Visibility.Visible;
    }

    private void Lock_Click(object sender, RoutedEventArgs e)
    {
        _repo.Lock();
        DocList.ItemsSource = null;
        PinBox.Clear();
        SearchBox.Text = "";
        VaultPanel.Visibility = Visibility.Collapsed;
        LoginPanel.Visibility = Visibility.Visible;
        PinBox.Focus();
    }

    private bool FilterPredicate(object obj)
    {
        if (obj is not Document d) return false;
        string q = SearchBox.Text?.Trim() ?? "";
        bool matches = q.Length == 0 ||
            d.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            d.Number.Contains(q, StringComparison.OrdinalIgnoreCase) ||
            d.TypeLabel.Contains(q, StringComparison.OrdinalIgnoreCase);
        bool pass = FilterBox.SelectedIndex switch
        {
            1 => d.IsExpired,
            2 => d.HasExpiry && !d.IsExpired && DateTime.TryParse(d.ExpiryDate, out var ex) && ex.Date <= DateTime.Today.AddDays(30),
            3 => !d.HasExpiry,
            _ => true,
        };
        return matches && pass;
    }

    private void Search_Changed(object sender, TextChangedEventArgs e) => _view?.Refresh();
    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => _view?.Refresh();

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var editor = new EditorWindow(new Document()) { Owner = this };
        if (editor.ShowDialog() == true) { _repo.Upsert(editor.Result); _view?.Refresh(); }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Document d) return;
        var editor = new EditorWindow(d.Clone()) { Owner = this };
        if (editor.ShowDialog() == true) { _repo.Upsert(editor.Result); _view?.Refresh(); }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Document d) return;
        if (MessageBox.Show($"¿Borrar \"{d.Name}\"?", "Confirmar", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            _repo.Delete(d.Id);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "SecureVault (*.securevault)|*.securevault",
            FileName = $"vault-{DateTime.Now:yyyyMMdd-HHmmss}.securevault",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            using var fs = File.Create(dlg.FileName);
            _repo.Export(fs);
            MessageBox.Show("Bóveda exportada correctamente.", "Exportar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al exportar: {ex.Message}", "Exportar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        var pinPrompt = new InputPinWindow("PIN de la bóveda a importar") { Owner = this };
        if (pinPrompt.ShowDialog() != true) return;
        string pin = pinPrompt.Pin;

        var dlg = new OpenFileDialog { Filter = "SecureVault (*.securevault)|*.securevault|Todos|*.*" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            using var fs = File.OpenRead(dlg.FileName);
            _repo.Import(fs, pin);
            _view?.Refresh();
            MessageBox.Show("Bóveda importada correctamente.", "Importar", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (IntegrityException ex)
        {
            MessageBox.Show(ex.Message, "Importar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error al importar: {ex.Message}", "Importar", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
