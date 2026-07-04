using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using IDPersonalSecure.Data;
using Microsoft.Win32;

namespace IDPersonalSecure;

public partial class EditorWindow : Window
{
    private const string Custom = "__CUSTOM__";
    public Document Result { get; }

    public byte[]? PendingAttachmentBytes { get; private set; }
    public string? PendingAttachmentName { get; private set; }
    public bool RemoveAttachment { get; private set; }

    public EditorWindow(Document doc)
    {
        InitializeComponent();
        Result = doc;

        HeaderText.Text = string.IsNullOrWhiteSpace(doc.Name) ? "Nuevo documento" : "Editar documento";

        TypeBox.ItemsSource = DocumentCatalog.Types
            .Append(new DocType(Custom, "Otro (personalizado)…", "XX")).ToList();
        bool known = DocumentCatalog.Types.Any(t => t.Code == doc.Type);
        TypeBox.SelectedValue = known ? doc.Type : Custom;
        if (!known && !string.IsNullOrEmpty(doc.Type)) CustomTypeBox.Text = doc.Type;

        NameBox.Text = doc.Name;
        CountryBox.Text = doc.Country;
        NumberBox.Text = doc.Number;
        IssueBox.Text = doc.IssueDate;
        HasExpiryBox.IsChecked = doc.HasExpiry;
        ExpiryBox.Text = doc.ExpiryDate;
        UrlBox.Text = doc.UrlSource;
        NotesBox.Text = doc.Notes;
        AttachLabel.Text = string.IsNullOrEmpty(doc.FileName) ? "(ninguno)" : doc.FileName;

        // Recordatorio guardado: "yyyy-MM-dd HH:mm"
        if (!string.IsNullOrWhiteSpace(doc.ReminderAt))
        {
            ReminderCheck.IsChecked = true;
            var parts = doc.ReminderAt.Split(' ');
            ReminderDateBox.Text = parts.Length > 0 ? parts[0] : "";
            ReminderTimeBox.Text = parts.Length > 1 ? parts[1] : "09:00";
        }

        IssueCal.DateSelected += d => { IssueBox.Text = d.ToString("yyyy-MM-dd"); IssuePopup.IsOpen = false; };
        ExpiryCal.DateSelected += d => { ExpiryBox.Text = d.ToString("yyyy-MM-dd"); ExpiryPopup.IsOpen = false; };
        ReminderCal.DateSelected += d => { ReminderDateBox.Text = d.ToString("yyyy-MM-dd"); ReminderPopup.IsOpen = false; };

        UpdateCustomTypeVisibility();
        UpdateExpiryVisibility();
    }

    private static DateTime? TryDate(string s) => DateTime.TryParse(s, out var d) ? d : null;

    private void Type_Changed(object sender, SelectionChangedEventArgs e) => UpdateCustomTypeVisibility();

    private void UpdateCustomTypeVisibility()
    {
        bool custom = (TypeBox.SelectedValue as string) == Custom;
        var vis = custom ? Visibility.Visible : Visibility.Collapsed;
        CustomTypeLabel.Visibility = vis;
        CustomTypeBox.Visibility = vis;
    }

    private void HasExpiry_Changed(object sender, RoutedEventArgs e) => UpdateExpiryVisibility();

    private void UpdateExpiryVisibility()
    {
        ExpiryPanel.Visibility = HasExpiryBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        UpdateReminderVisibility();
    }

    private void Reminder_Changed(object sender, RoutedEventArgs e) => UpdateReminderVisibility();

    private void UpdateReminderVisibility()
    {
        bool on = HasExpiryBox.IsChecked == true && ReminderCheck.IsChecked == true;
        ReminderRow.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
    }

    private void IssueCalendar_Click(object sender, RoutedEventArgs e) { IssueCal.SetDate(TryDate(IssueBox.Text)); IssuePopup.IsOpen = true; }
    private void ExpiryCalendar_Click(object sender, RoutedEventArgs e) { ExpiryCal.SetDate(TryDate(ExpiryBox.Text)); ExpiryPopup.IsOpen = true; }
    private void ReminderCalendar_Click(object sender, RoutedEventArgs e) { ReminderCal.SetDate(TryDate(ReminderDateBox.Text)); ReminderPopup.IsOpen = true; }

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

        if ((TypeBox.SelectedValue as string) == Custom)
        {
            string custom = CustomTypeBox.Text.Trim();
            Result.Type = custom.Length > 0 ? custom : "Otro";
        }
        else Result.Type = (TypeBox.SelectedValue as string) ?? "CC";

        Result.Country = CountryBox.Text.Trim().ToUpperInvariant();
        Result.Number = NumberBox.Text.Trim();
        Result.IssueDate = IssueBox.Text.Trim();
        Result.HasExpiry = HasExpiryBox.IsChecked == true;
        Result.ExpiryDate = Result.HasExpiry ? ExpiryBox.Text.Trim() : "";
        Result.UrlSource = UrlBox.Text.Trim();
        Result.Notes = NotesBox.Text.Trim();

        string reminder = "";
        if (Result.HasExpiry && ReminderCheck.IsChecked == true)
        {
            string d = ReminderDateBox.Text.Trim();
            string t = ReminderTimeBox.Text.Trim();
            if (d.Length > 0) reminder = d + " " + (t.Length > 0 ? t : "09:00");
        }
        Result.ReminderAt = reminder;

        if (PendingAttachmentBytes != null) Result.FileName = PendingAttachmentName ?? "";
        else if (RemoveAttachment) Result.FileName = "";

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
