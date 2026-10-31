namespace CommunicationApp.Services;

public sealed class ForwardingStatus(IPreferences preferences)
{
    private string? _error;

    public event Action? Changed;

    public string Text
    {
        get
        {
            if (_error != null)
                return _error;

            var count = preferences.Get("lastForwardedCount", 0);
            if (count == 0)
                return "Nothing forwarded yet";

            var time = preferences.Get("lastForwardedAt", DateTime.Now);
            return $"Last forwarded: {count} {(count == 1 ? "message" : "messages")} at {Format(time)}";
        }
    }

    public void Succeeded(int sent)
    {
        if (sent == 0 && _error == null)
            return;

        SaveForwarded(sent);
        _error = null;
        Changed?.Invoke();
    }

    public void Failed(int sent, string reason)
    {
        SaveForwarded(sent);
        _error = $"Last check failed at {Format(DateTime.Now)}: {reason}";
        Changed?.Invoke();
    }

    private void SaveForwarded(int sent)
    {
        if (sent == 0)
            return;

        preferences.Set("lastForwardedCount", sent);
        preferences.Set("lastForwardedAt", DateTime.Now);
    }

    private static string Format(DateTime time) =>
        time.Date == DateTime.Today ? time.ToString("HH:mm") : time.ToString("dd MMM HH:mm");
}
