using System.Xml.Linq;

namespace OcrSnip.Tests;

public sealed class AppManifestTests
{
    [Fact]
    public void AppManifest_DeclaresAsInvokerAndProjectDeclaresPerMonitorV2()
    {
        var repoRoot = FindRepoRoot();
        var manifest = XDocument.Load(Path.Combine(repoRoot, "src", "OcrSnip.App", "app.manifest"));
        var project = XDocument.Load(Path.Combine(repoRoot, "src", "OcrSnip.App", "OcrSnip.App.csproj"));
        var text = manifest.ToString(SaveOptions.DisableFormatting);
        var projectText = project.ToString(SaveOptions.DisableFormatting);

        Assert.Contains("asInvoker", text);
        Assert.Contains("PerMonitorV2", projectText);
    }

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
