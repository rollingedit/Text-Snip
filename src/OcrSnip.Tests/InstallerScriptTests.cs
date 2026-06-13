namespace OcrSnip.Tests;

public sealed class InstallerScriptTests
{
    [Fact]
    public void Installer_DoesNotRecursivelyDeleteInstallDirectory()
    {
        var script = File.ReadAllText(GetRepoPath("installer", "OcrSnip.iss"));

        Assert.DoesNotContain("Name: \"{app}\\*\"", script);
        Assert.DoesNotContain("[UninstallDelete]", script);
        Assert.DoesNotContain("Name: \"{app}\"", script);
    }

    [Fact]
    public void Installer_RemovesStartupRegistrationOnUninstall()
    {
        var script = File.ReadAllText(GetRepoPath("installer", "OcrSnip.iss"));

        Assert.Contains("ValueName: \"OcrSnip\"; ValueData:", script);
        Assert.Contains("Flags: uninsdeletevalue", script);
    }

    [Fact]
    public void Installer_UsesTextSnipBrandingAndStartupText()
    {
        var script = File.ReadAllText(GetRepoPath("installer", "OcrSnip.iss"));

        Assert.Contains("#define MyAppName \"Text Snip\"", script);
        Assert.Contains("#define MyAppVersion \"1.0.0\"", script);
        Assert.Contains("OutputBaseFilename=Text-Snip-Setup-x64", script);
        Assert.Contains("Description: \"Start Text Snip at startup\"", script);
        Assert.Contains("Description: \"Launch Text Snip\"", script);
        Assert.Contains("ArchitecturesAllowed=x64compatible", script);
        Assert.Contains("ArchitecturesInstallIn64BitMode=x64compatible", script);
        Assert.DoesNotContain("when I sign in", script);
    }

    [Fact]
    public void Installer_FailsEarlyWhenVCRuntimeNeedsElevation()
    {
        var script = File.ReadAllText(GetRepoPath("installer", "OcrSnip.iss"));

        Assert.Contains("if NeedsVCRedist and not IsAdminInstallMode then", script);
        Assert.Contains("Text Snip needs the Microsoft Visual C++ Runtime", script);
        Assert.Contains("install the x64 Visual C++ Runtime first", script);
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
