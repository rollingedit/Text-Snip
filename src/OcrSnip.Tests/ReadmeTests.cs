namespace OcrSnip.Tests;

public sealed class ReadmeTests
{
    [Fact]
    public void Readme_DoesNotRequireIgnoredDotnetDirectory()
    {
        var readme = File.ReadAllText(GetRepoPath("README.md"));

        Assert.Contains("dotnet test OcrSnip.slnx -c Release", readme);
        Assert.DoesNotContain(@".\.dotnet\dotnet.exe test", readme);
    }

    [Fact]
    public void Readme_DocumentsDoctorMode()
    {
        var readme = File.ReadAllText(GetRepoPath("README.md"));

        Assert.Contains(".\\OcrSnip.App.exe --doctor", readme);
        Assert.Contains("does not capture screenshots", readme);
        Assert.Contains("or log recognized text", readme);
    }

    private static string GetRepoPath(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OcrSnip.slnx")))
            {
                return Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repo root.");
    }
}
