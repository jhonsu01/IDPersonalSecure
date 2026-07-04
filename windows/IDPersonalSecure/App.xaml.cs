using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IDPersonalSecure.Sharing;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace IDPersonalSecure;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--selftest-watermark")) { RunSelfTest(); Shutdown(); return; }
        new MainWindow().Show();
    }

    /// <summary>Valida en runtime la marca de agua sobre imagen y PDF (uso interno de CI/dev).</summary>
    private static void RunSelfTest()
    {
        string dir = Path.Combine(Path.GetTempPath(), "idps-selftest");
        Directory.CreateDirectory(dir);
        try
        {
            var info = new ShareInfo("Prueba apertura cuenta", WatermarkService.NewShareId(),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm"));

            // Imagen de prueba (PNG vía WPF).
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.LightGray, null, new Rect(0, 0, 600, 400));
                dc.DrawText(new FormattedText("DOC DE PRUEBA", CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface("Segoe UI"), 40, Brushes.Black, 1.0), new Point(60, 180));
            }
            var rtb = new RenderTargetBitmap(600, 400, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv);
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            byte[] imgBytes;
            using (var ms = new MemoryStream()) { enc.Save(ms); imgBytes = ms.ToArray(); }
            var (imgOut, imgExt) = WatermarkService.Apply(imgBytes, "test.png", info);
            File.WriteAllBytes(Path.Combine(dir, $"image-watermarked{imgExt}"), imgOut);

            // PDF de prueba (PdfSharp).
            var pdf = new PdfDocument();
            var page = pdf.AddPage();
            using (var g = XGraphics.FromPdfPage(page))
                g.DrawString("DOC PDF DE PRUEBA", new XFont("Arial", 24), XBrushes.Black, new XPoint(60, 80));
            byte[] pdfBytes;
            using (var ms = new MemoryStream()) { pdf.Save(ms); pdfBytes = ms.ToArray(); }
            var (pdfOut, pdfExt) = WatermarkService.Apply(pdfBytes, "test.pdf", info);
            File.WriteAllBytes(Path.Combine(dir, $"pdf-watermarked{pdfExt}"), pdfOut);

            File.WriteAllText(Path.Combine(dir, "ok.txt"),
                $"OK\nshareId={info.ShareId}\nimg={imgOut.Length} bytes\npdf={pdfOut.Length} bytes\n");
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(dir, "error.txt"), ex.ToString());
        }
    }
}
