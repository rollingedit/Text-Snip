namespace OcrSnip.App;

public static class SingleInstanceActivation
{
    public const string ResidentMutexName = "Global\\OcrSnip_1B2F1F57_13E7_4F40_9E7D_Resident";
    public const string ShowWindowEventName = "Global\\OcrSnip_1B2F1F57_13E7_4F40_9E7D_ShowWindow";

    public static EventWaitHandle CreateShowWindowEvent(string? eventName = null)
    {
        return new EventWaitHandle(false, EventResetMode.AutoReset, eventName ?? ShowWindowEventName);
    }

    public static bool SignalExistingInstance(string? eventName = null)
    {
        try
        {
            using var existing = EventWaitHandle.OpenExisting(eventName ?? ShowWindowEventName);
            existing.Set();
            return true;
        }
        catch (WaitHandleCannotBeOpenedException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
