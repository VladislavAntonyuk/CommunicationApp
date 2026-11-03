namespace CommunicationApp.Services;

public sealed class MessageForwarder(ISmsSender smsSender, ForwardingStatus status, IPreferences preferences) : IMessageHandler
{
    public Task Handle(string text)
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

    public void Connected() => status.Succeeded(0);

    public void Failed(string reason) => status.Failed(0, reason);
}
