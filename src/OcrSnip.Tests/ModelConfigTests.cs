using OcrSnip.Ocr;

namespace OcrSnip.Tests;

public sealed class ModelConfigTests
{
    [Fact]
    public void LoadCharacters_ReadsBundledRecognizerDictionary()
    {
        var repoRoot = FindRepoRoot();
        var config = Path.Combine(repoRoot, "assets", "models", "ppocrv6-small-rec", "inference.yml");

        var characters = ModelConfig.LoadCharacters(config);

        Assert.Equal(18709, characters.Length);
        Assert.Contains("A", characters);
        Assert.Contains("_", characters);
        Assert.Contains("/", characters);
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
