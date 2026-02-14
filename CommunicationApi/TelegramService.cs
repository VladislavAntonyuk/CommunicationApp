namespace CommunicationApi;

internal class TelegramService
{
    public Task Send(string recipient, string text)
    {
        return Task.CompletedTask;
    }
}