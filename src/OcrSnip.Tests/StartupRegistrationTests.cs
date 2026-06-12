using Microsoft.Win32;
using OcrSnip.App.Settings;

namespace OcrSnip.Tests;

public sealed class StartupRegistrationTests : IDisposable
{
    public void Dispose()
    {
        StartupRegistration.Apply(false);
    }

    [Fact]
    public void Apply_RegistersAndRemovesCurrentExecutable()
    {
        StartupRegistration.Apply(false);

        StartupRegistration.Apply(true);
        Assert.True(StartupRegistration.IsRegistered(Environment.ProcessPath));

        StartupRegistration.Apply(false);
        Assert.False(StartupRegistration.IsRegistered(Environment.ProcessPath));
    }

    [Fact]
    public void IsRegistered_ReturnsFalseForMissingValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true)
            ?? Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
        key?.DeleteValue("OcrSnip", throwOnMissingValue: false);

        Assert.False(StartupRegistration.IsRegistered(Environment.ProcessPath));
    }
}
