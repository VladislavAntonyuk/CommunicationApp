namespace CommunicationApp;

public static class ForwardingStatus
{
    private static string? _error;

    public static event Action? Changed;

    public static string Text
    {
        get
        {
            if (_error != null)
                return _error;

            var count = Preferences.Get("lastForwardedCount", 0);
            if (count == 0)
                return "Nothing forwarded yet";

            var time = Preferences.Get("lastForwardedAt", DateTime.Now);
            return $"Last forwarded: {count} {(count == 1 ? "message" : "messages")} at {Format(time)}";
        }
    }

    public static void Succeeded(int sent)
    {
        if (sent == 0 && _error == null)
            return;

        SaveForwarded(sent);
        _error = null;
        Changed?.Invoke();
    }

    public static void Failed(int sent, string reason)
    {
        SaveForwarded(sent);
        _error = $"Last check failed at {Format(DateTime.Now)}: {reason}";
        Changed?.Invoke();
    }

    private static void SaveForwarded(int sent)
    {
        if (sent == 0)
            return;

        Preferences.Set("lastForwardedCount", sent);
        Preferences.Set("lastForwardedAt", DateTime.Now);
    }

    private static string Format(DateTime time) =>
        time.Date == DateTime.Today ? time.ToString("HH:mm") : time.ToString("dd MMM HH:mm");
}
