using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IDPersonalSecure;

/// <summary>Calendario propio con colores explícitos (legible en tema oscuro).</summary>
public partial class CalendarControl : UserControl
{
    private static readonly Brush TextB = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0));
    private static readonly Brush MutedB = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B));
    private static readonly Brush AccentB = new SolidColorBrush(Color.FromRgb(0x63, 0x66, 0xF1));
    private static readonly Brush TodayB = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
    private static readonly CultureInfo Es = new("es-ES");

    private DateTime _view = DateTime.Today;
    private DateTime? _selected;

    public event Action<DateTime>? DateSelected;

    public CalendarControl()
    {
        InitializeComponent();
        BuildDow();
        Render();
    }

    public void SetDate(DateTime? d)
    {
        _selected = d;
        var anchor = d ?? DateTime.Today;
        _view = new DateTime(anchor.Year, anchor.Month, 1);
        Render();
    }

    private void BuildDow()
    {
        DowGrid.Children.Clear();
        foreach (var s in new[] { "D", "L", "M", "M", "J", "V", "S" })
            DowGrid.Children.Add(new TextBlock { Text = s, Foreground = MutedB, TextAlignment = TextAlignment.Center, FontSize = 11 });
    }

    private void Prev_Click(object sender, RoutedEventArgs e) { _view = _view.AddMonths(-1); Render(); }
    private void Next_Click(object sender, RoutedEventArgs e) { _view = _view.AddMonths(1); Render(); }

    private void Render()
    {
        MonthLabel.Text = _view.ToString("MMMM yyyy", Es);
        DaysGrid.Children.Clear();
        var first = new DateTime(_view.Year, _view.Month, 1);
        var start = first.AddDays(-(int)first.DayOfWeek); // domingo primero
        var style = (Style)FindResource("DayButton");
        for (int i = 0; i < 42; i++)
        {
            var day = start.AddDays(i);
            bool inMonth = day.Month == _view.Month;
            bool isSel = _selected.HasValue && day.Date == _selected.Value.Date;
            bool isToday = day.Date == DateTime.Today;
            var btn = new Button
            {
                Style = style,
                Content = day.Day.ToString(),
                Foreground = inMonth ? TextB : MutedB,
                Background = isSel ? AccentB : (isToday ? TodayB : Brushes.Transparent),
                Tag = day,
            };
            btn.Click += Day_Click;
            DaysGrid.Children.Add(btn);
        }
    }

    private void Day_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is DateTime d)
        {
            _selected = d;
            Render();
            DateSelected?.Invoke(d);
        }
    }
}
