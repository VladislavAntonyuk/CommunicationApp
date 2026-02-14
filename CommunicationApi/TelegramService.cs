namespace CommunicationApi;

internal class TelegramService:ISender
{
    public Task Send(string text)
    {
        return Task.CompletedTask;
    }
}