using System.Security.Cryptography;
using System.Text.Json;

namespace OcrSnip.Tests;

public sealed class ModelHashManifestTests
{
    [Fact]
    public void BundledModels_MatchPinnedHashes()
    {
        var repoRoot = FindRepoRoot();
        var manifestPath = Path.Combine(repoRoot, "tools", "model-hashes.json");
        using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));

        foreach (var model in document.RootElement.GetProperty("models").EnumerateArray())
        {
            var relativePath = model.GetProperty("path").GetString()!;
            var expectedHash = model.GetProperty("sha256").GetString()!;
            var path = Path.Combine(repoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(File.Exists(path), $"Missing model asset: {relativePath}");
            using var stream = File.OpenRead(path);
            var actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            Assert.Equal(expectedHash, actual);
        }
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
