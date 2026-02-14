namespace CommunicationApi;

internal class EmailService:ISender
{
    public Task Send(string text)
    {
        return Task.CompletedTask;
    }
}