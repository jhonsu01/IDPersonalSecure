using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace IDPersonalSecure.Sharing;

/// <summary>Datos que se estampan en la copia compartida.</summary>
public sealed record ShareInfo(string Tramite, string ShareId, string DateTime);

/// <summary>
/// Aplica marca de agua (trámite + ID único + fecha/hora) a imágenes (WPF) y PDF (PdfSharp).
/// </summary>
public static class WatermarkService
{
    static WatermarkService()
    {
        // PdfSharp necesita un resolvedor de fuentes: usamos Arial del sistema.
        GlobalFontSettings.FontResolver ??= new WinFontResolver();
    }

    public static readonly string[] ImageExtensions =
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tif", ".tiff" };

    public static bool IsSupported(string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext == ".pdf" || Array.IndexOf(ImageExtensions, ext) >= 0;
    }

    /// <summary>Devuelve (bytes, extensión) de la copia con marca de agua.</summary>
    public static (byte[] data, string ext) Apply(byte[] input, string originalName, ShareInfo info)
    {
        string ext = Path.GetExtension(originalName).ToLowerInvariant();
        return ext == ".pdf"
            ? (ApplyPdf(input, info), ".pdf")
            : (ApplyImage(input, info), ".png");
    }

    // ── Imagen (WPF, sin System.Drawing) ─────────────────────────────────
    private static byte[] ApplyImage(byte[] input, ShareInfo info)
    {
        var src = Decode(input);
        int w = src.PixelWidth, h = src.PixelHeight;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(src, new Rect(0, 0, w, h));
            DrawWatermark(dc, w, h, info);
        }
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    private static BitmapSource Decode(byte[] input)
    {
        var img = new BitmapImage();
        using var ms = new MemoryStream(input);
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.StreamSource = ms;
        img.EndInit();
        img.Freeze();
        return img;
    }

    private static void DrawWatermark(DrawingContext dc, int w, int h, ShareInfo info)
    {
        var typeface = new Typeface("Segoe UI");
        var tileBrush = new SolidColorBrush(Color.FromArgb(70, 190, 40, 40));
        double fs = Math.Max(16, Math.Min(w, h) / 18.0);

        dc.PushTransform(new RotateTransform(-30, w / 2.0, h / 2.0));
        double stepX = fs * (info.Tramite.Length + 6) * 0.62;
        double stepY = fs * 3.2;
        for (double y = -h; y < 2 * h; y += stepY)
            for (double x = -w; x < 2 * w; x += stepX)
                dc.DrawText(Text(info.Tramite, fs, tileBrush, typeface), new Point(x, y));
        dc.Pop();

        // Banda inferior con ID + fecha/hora + trámite.
        double band = Math.Max(30, h * 0.07);
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)), null, new Rect(0, h - band, w, band));
        string footer = $"ID: {info.ShareId}   |   {info.DateTime}   |   {info.Tramite}";
        var ft = Text(footer, Math.Max(11, band * 0.42), Brushes.White, typeface);
        ft.MaxTextWidth = w - 20;
        ft.MaxLineCount = 1;
        ft.Trimming = TextTrimming.CharacterEllipsis;
        dc.DrawText(ft, new Point(10, h - band + band * 0.24));
    }

    private static FormattedText Text(string s, double size, Brush brush, Typeface tf) =>
        new(s, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, size, brush, 1.0);

    // ── PDF (PdfSharp) ───────────────────────────────────────────────────
    private static byte[] ApplyPdf(byte[] input, ShareInfo info)
    {
        using var msIn = new MemoryStream(input);
        var pdf = PdfReader.Open(msIn, PdfDocumentOpenMode.Modify);
        foreach (PdfPage page in pdf.Pages)
        {
            using var gfx = XGraphics.FromPdfPage(page);
            double pw = page.Width.Point, ph = page.Height.Point;
            var tileFont = new XFont("Arial", Math.Max(18, Math.Min(pw, ph) / 16));
            var tileBrush = new XSolidBrush(XColor.FromArgb(55, 190, 40, 40));

            XGraphicsState state = gfx.Save();
            gfx.RotateAtTransform(-30, new XPoint(pw / 2, ph / 2));
            double stepX = tileFont.Size * (info.Tramite.Length + 6) * 0.62;
            double stepY = tileFont.Size * 3.4;
            for (double y = -ph; y < 2 * ph; y += stepY)
                for (double x = -pw; x < 2 * pw; x += stepX)
                    gfx.DrawString(info.Tramite, tileFont, tileBrush, new XPoint(x, y));
            gfx.Restore(state);

            double band = 22;
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(160, 0, 0, 0)), 0, ph - band, pw, band);
            var footFont = new XFont("Arial", Math.Max(8, Math.Min(pw, ph) / 48));
            string footer = $"ID: {info.ShareId}   |   {info.DateTime}   |   {info.Tramite}";
            gfx.DrawString(footer, footFont, XBrushes.White,
                new XRect(6, ph - band, pw - 12, band), XStringFormats.CenterLeft);
        }
        using var msOut = new MemoryStream();
        pdf.Save(msOut);
        return msOut.ToArray();
    }

    /// <summary>Genera un ID de seguimiento único y legible.</summary>
    public static string NewShareId() =>
        "IDPS-" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}

/// <summary>Resuelve fuentes para PdfSharp cargando Arial desde C:\Windows\Fonts.</summary>
internal sealed class WinFontResolver : IFontResolver
{
    public byte[]? GetFont(string faceName)
    {
        string dir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        string file = faceName.Contains("Bold", StringComparison.OrdinalIgnoreCase) ? "arialbd.ttf" : "arial.ttf";
        string path = Path.Combine(dir, file);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }

    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        => new FontResolverInfo(isBold ? "Arial#Bold" : "Arial");
}
