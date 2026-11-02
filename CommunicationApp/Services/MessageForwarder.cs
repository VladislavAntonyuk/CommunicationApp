namespace CommunicationApp.Services;

public sealed class MessageForwarder(MessageListener listener, ISmsSender smsSender, ForwardingStatus status, IPreferences preferences)
{
    public Task Listen(CancellationToken token) =>
        listener.Run(Forward, () => status.Succeeded(0), reason => status.Failed(0, reason), token);

    private Task Forward(string text)
    {
        try
        {
            smsSender.Send(preferences.Get("from", ""), text);
        }
        catch (Exception ex)
        {
            var reason = $"Could not send SMS: {ex.Message}";
            status.Failed(0, reason);
            throw new InvalidOperationException(reason, ex);
        }

        status.Succeeded(1);
        return Task.CompletedTask;
    }
}
