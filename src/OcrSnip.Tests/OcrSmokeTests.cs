using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using OcrSnip.Ocr;

namespace OcrSnip.Tests;

public sealed class OcrSmokeTests
{
    [Fact]
    public async Task GeneratedTextImage_RecognizesText()
    {
        var repoRoot = FindRepoRoot();
        var engine = new OcrEngine(new ModelPaths(
            Path.Combine(repoRoot, "assets", "models", "ppocrv6-small-det", "inference.onnx"),
            Path.Combine(repoRoot, "assets", "models", "ppocrv6-small-det", "inference.yml"),
            Path.Combine(repoRoot, "assets", "models", "ppocrv6-small-rec", "inference.onnx"),
            Path.Combine(repoRoot, "assets", "models", "ppocrv6-small-rec", "inference.yml")));

        using var bitmap = new Bitmap(640, 220);
        using (var graphics = Graphics.FromImage(bitmap))
        using (var font = new Font("Arial", 54, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel))
        {
            graphics.Clear(Color.White);
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            graphics.DrawString("OCR TEST", font, Brushes.Black, new PointF(30, 70));
        }

        var source = ToBitmapSource(bitmap);
        var result = await engine.RecognizeAsync(source, CancellationToken.None);

        Assert.Contains("OCR", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("TEST", result.Text, StringComparison.OrdinalIgnoreCase);
    }

    private static BitmapSource ToBitmapSource(Bitmap bitmap)
    {
        var handle = bitmap.GetHbitmap();
        try
        {
            var source = Imaging.CreateBitmapSourceFromHBitmap(handle, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(handle);
        }
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OcrSnip.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
