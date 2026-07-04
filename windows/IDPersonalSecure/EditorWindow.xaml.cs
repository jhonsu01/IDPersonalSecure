using System.IO;
using System.Windows;
using System.Windows.Controls;
using IDPersonalSecure.Data;
using Microsoft.Win32;

namespace IDPersonalSecure;

public partial class EditorWindow : Window
{
    public Document Result { get; }

    // Intención de adjunto (la aplica MainWindow tras guardar).
    public byte[]? PendingAttachmentBytes { get; private set; }
    public string? PendingAttachmentName { get; private set; }
    public bool RemoveAttachment { get; private set; }

    public EditorWindow(Document doc)
    {
        InitializeComponent();
        Result = doc;

        HeaderText.Text = string.IsNullOrWhiteSpace(doc.Name) ? "Nuevo documento" : "Editar documento";
        TypeBox.ItemsSource = DocumentCatalog.Types;
        TypeBox.SelectedValue = doc.Type;

        NameBox.Text = doc.Name;
        CountryBox.Text = doc.Country;
        NumberBox.Text = doc.Number;
        IssueBox.Text = doc.IssueDate;
        HasExpiryBox.IsChecked = doc.HasExpiry;
        ExpiryBox.Text = doc.ExpiryDate;
        UrlBox.Text = doc.UrlSource;
        NotesBox.Text = doc.Notes;
        AttachLabel.Text = string.IsNullOrEmpty(doc.FileName) ? "(ninguno)" : doc.FileName;
        UpdateExpiryVisibility();
    }

    private void HasExpiry_Changed(object sender, RoutedEventArgs e) => UpdateExpiryVisibility();

    private void UpdateExpiryVisibility()
    {
        var vis = HasExpiryBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ExpiryLabel.Visibility = vis;
        ExpiryRow.Visibility = vis;
    }

    // ── Calendarios ──────────────────────────────────────────────────────
    private void IssueCalendar_Click(object sender, RoutedEventArgs e) => OpenCalendar(IssueBox, IssueCal, IssuePopup);
    private void ExpiryCalendar_Click(object sender, RoutedEventArgs e) => OpenCalendar(ExpiryBox, ExpiryCal, ExpiryPopup);

    private static void OpenCalendar(TextBox box, Calendar cal, System.Windows.Controls.Primitives.Popup popup)
    {
        if (DateTime.TryParse(box.Text, out var d)) { cal.SelectedDate = d; cal.DisplayDate = d; }
        popup.IsOpen = true;
    }

    private void IssueCal_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IssueCal.SelectedDate is DateTime d) { IssueBox.Text = d.ToString("yyyy-MM-dd"); IssuePopup.IsOpen = false; }
    }

    private void ExpiryCal_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (ExpiryCal.SelectedDate is DateTime d) { ExpiryBox.Text = d.ToString("yyyy-MM-dd"); ExpiryPopup.IsOpen = false; }
    }

    // ── Adjuntos ─────────────────────────────────────────────────────────
    private void Attach_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Documentos (PDF/imagen)|*.pdf;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.tif;*.tiff|Todos|*.*",
        };
        if (dlg.ShowDialog() != true) return;
        PendingAttachmentBytes = File.ReadAllBytes(dlg.FileName);
        PendingAttachmentName = Path.GetFileName(dlg.FileName);
        RemoveAttachment = false;
        AttachLabel.Text = PendingAttachmentName;
    }

    private void RemoveAttach_Click(object sender, RoutedEventArgs e)
    {
        PendingAttachmentBytes = null;
        PendingAttachmentName = null;
        RemoveAttachment = true;
        AttachLabel.Text = "(ninguno)";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Result.Name = NameBox.Text.Trim();
        Result.Type = (TypeBox.SelectedValue as string) ?? "CC";
        Result.Country = CountryBox.Text.Trim().ToUpperInvariant();
        Result.Number = NumberBox.Text.Trim();
        Result.IssueDate = IssueBox.Text.Trim();
        Result.HasExpiry = HasExpiryBox.IsChecked == true;
        Result.ExpiryDate = Result.HasExpiry ? ExpiryBox.Text.Trim() : "";
        Result.UrlSource = UrlBox.Text.Trim();
        Result.Notes = NotesBox.Text.Trim();

        if (PendingAttachmentBytes != null) Result.FileName = PendingAttachmentName ?? "";
        else if (RemoveAttachment) Result.FileName = "";

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
