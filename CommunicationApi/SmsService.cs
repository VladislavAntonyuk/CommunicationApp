namespace CommunicationApi;

internal class SmsService:ISender
{
    public Task Send(string text)
    {
        return Task.CompletedTask;
    }
}