using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using IDPersonalSecure.Data;
using IDPersonalSecure.Sharing;
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
        if (editor.ShowDialog() == true) { _repo.Upsert(editor.Result); ApplyAttachment(editor); _view?.Refresh(); }
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Document d) return;
        var editor = new EditorWindow(d.Clone()) { Owner = this };
        if (editor.ShowDialog() == true) { _repo.Upsert(editor.Result); ApplyAttachment(editor); _view?.Refresh(); }
    }

    private void ApplyAttachment(EditorWindow editor)
    {
        if (editor.PendingAttachmentBytes != null)
            _repo.SaveAttachment(editor.Result.Id, editor.PendingAttachmentBytes);
        else if (editor.RemoveAttachment)
            _repo.DeleteAttachment(editor.Result.Id);
    }

    private void History_Click(object sender, RoutedEventArgs e) =>
        new HistoryWindow(_repo) { Owner = this }.ShowDialog();

    private void Share_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not Document d) return;
        if (!_repo.HasAttachment(d.Id) || string.IsNullOrEmpty(d.FileName))
        {
            MessageBox.Show("Este documento no tiene un adjunto para compartir.\nEdítalo y adjunta un PDF o imagen primero.",
                "Compartir", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var opts = new ShareOptionsWindow { Owner = this };
        if (opts.ShowDialog() != true) return;
        bool wm = opts.ApplyWatermark;
        if (wm && !WatermarkService.IsSupported(d.FileName))
        {
            MessageBox.Show("El adjunto no es una imagen ni un PDF, no se le puede aplicar marca de agua.\nCompártelo sin marca de agua.",
                "Compartir", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            byte[] data = _repo.ReadAttachment(d.Id)!;
            string shareId = WatermarkService.NewShareId();
            string when = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            byte[] outBytes;
            string ext;
            if (wm)
            {
                (outBytes, ext) = WatermarkService.Apply(data, d.FileName, new ShareInfo(opts.Tramite, shareId, when));
            }
            else
            {
                outBytes = data;
                ext = Path.GetExtension(d.FileName);
                if (string.IsNullOrEmpty(ext)) ext = ".bin";
            }

            var save = new SaveFileDialog
            {
                FileName = $"{SafeName(d.Name)}-{shareId}{ext}",
                Filter = $"Archivo (*{ext})|*{ext}|Todos|*.*",
            };
            if (save.ShowDialog() != true) return;
            File.WriteAllBytes(save.FileName, outBytes);
            try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{save.FileName}\"") { UseShellExecute = true }); }
            catch { /* opcional */ }

            _repo.AddShareRecord(new ShareRecord
            {
                Id = shareId, DocId = d.Id, DocName = d.Name,
                Tramite = wm ? opts.Tramite : "", DateTime = when,
                Recipient = opts.Recipient, Watermarked = wm,
            });

            MessageBox.Show(
                $"Copia generada{(wm ? " con marca de agua" : " (sin marca de agua)")}.\n\nID de seguimiento: {shareId}\nFecha: {when}" +
                (string.IsNullOrWhiteSpace(opts.Recipient) ? "" : $"\nDestinatario: {opts.Recipient}") +
                "\n\nQueda registrada en el Historial.",
                "Compartir", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"No se pudo generar la copia: {ex.Message}", "Compartir",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string SafeName(string name)
    {
        string s = Regex.Replace(name, "[^\\w\\-]+", "_").Trim('_');
        return string.IsNullOrEmpty(s) ? "documento" : s;
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
