using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;
using OcrSnip.Ocr;

if (args.Length < 1)
{
    Console.Error.WriteLine("Usage: OcrSnip.Tools.OcrCli <image-path> [--json] [--small-text-boost <Auto|Off|Scale150|Scale200|Scale300>]");
    return 2;
}

var imagePath = Path.GetFullPath(args[0]);
var json = args.Contains("--json", StringComparer.OrdinalIgnoreCase);
if (!File.Exists(imagePath))
{
    Console.Error.WriteLine($"Image not found: {imagePath}");
    return 2;
}

var repoRoot = FindRepoRoot(AppContext.BaseDirectory);
var modelPaths = new ModelPaths(
    Path.Combine(repoRoot, "assets", "models", "ppocrv6-small-det", "inference.onnx"),
    Path.Combine(repoRoot, "assets", "models", "ppocrv6-small-det", "inference.yml"),
    Path.Combine(repoRoot, "assets", "models", "ppocrv6-small-rec", "inference.onnx"),
    Path.Combine(repoRoot, "assets", "models", "ppocrv6-small-rec", "inference.yml"));

var bitmap = new BitmapImage();
bitmap.BeginInit();
bitmap.CacheOption = BitmapCacheOption.OnLoad;
bitmap.UriSource = new Uri(imagePath);
bitmap.EndInit();
bitmap.Freeze();

using var engine = new OcrEngine(modelPaths);
engine.Options.SmallTextBoost = ParseSmallTextBoost(args);
var stopwatch = Stopwatch.StartNew();
var result = await engine.RecognizeAsync(bitmap, CancellationToken.None);
stopwatch.Stop();

if (json)
{
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        image = imagePath,
        elapsedMs = stopwatch.ElapsedMilliseconds,
        text = result.Text,
        diagnostics = result.Diagnostics,
        lines = result.Lines.Select(line => new { line.Text, line.Confidence, line.Bounds })
    }, new JsonSerializerOptions { WriteIndented = true }));
}
else
{
    Console.WriteLine(result.Text);
    Console.Error.WriteLine($"Elapsed: {stopwatch.ElapsedMilliseconds} ms");
}

return string.IsNullOrWhiteSpace(result.Text) ? 1 : 0;

static string FindRepoRoot(string start)
{
    var directory = new DirectoryInfo(start);
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

static SmallTextBoostMode ParseSmallTextBoost(string[] args)
{
    var index = Array.FindIndex(args, argument => string.Equals(argument, "--small-text-boost", StringComparison.OrdinalIgnoreCase));
    if (index < 0)
    {
        return SmallTextBoostMode.Auto;
    }

    if (index + 1 >= args.Length || !Enum.TryParse<SmallTextBoostMode>(args[index + 1], ignoreCase: true, out var mode))
    {
        throw new ArgumentException("Invalid --small-text-boost value.");
    }

    return mode;
}
