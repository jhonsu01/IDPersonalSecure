using System.Windows;
using IDPersonalSecure.Data;

namespace IDPersonalSecure;

public partial class EditorWindow : Window
{
    public Document Result { get; }

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
        UpdateExpiryVisibility();
    }

    private void HasExpiry_Changed(object sender, RoutedEventArgs e) => UpdateExpiryVisibility();

    private void UpdateExpiryVisibility()
    {
        bool on = HasExpiryBox.IsChecked == true;
        ExpiryLabel.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        ExpiryBox.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
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
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
