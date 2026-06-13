using OcrSnip.App;

namespace OcrSnip.Tests;

public sealed class SingleInstanceActivationTests
{
    [Fact]
    public void SignalExistingInstance_ReturnsFalseWhenEventDoesNotExist()
    {
        var eventName = $@"Local\OcrSnip_Test_{Guid.NewGuid():N}";

        Assert.False(SingleInstanceActivation.SignalExistingInstance(eventName));
    }

    [Fact]
    public void SignalExistingInstance_SetsExistingEvent()
    {
        var eventName = $@"Local\OcrSnip_Test_{Guid.NewGuid():N}";
        using var activationEvent = SingleInstanceActivation.CreateShowWindowEvent(eventName);

        Assert.True(SingleInstanceActivation.SignalExistingInstance(eventName));
        Assert.True(activationEvent.WaitOne(TimeSpan.FromSeconds(1)));
    }
}
