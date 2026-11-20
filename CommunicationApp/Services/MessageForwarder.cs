namespace CommunicationApp.Services;

public sealed class MessageForwarder(ISmsSender smsSender, ForwardingStatus status) : IMessageHandler
{
    public async Task Handle(string to, string text)
    {
        try
        {
            await smsSender.Send(to, text);
        }
        catch (Exception ex)
        {
            status.Failed(0, $"Could not send SMS: {ex.Message}");
            throw;
        }

        status.Succeeded(1);
    }

    public void Connected() => status.Succeeded(0);

    public void Failed(string reason) => status.Failed(0, reason);
}
