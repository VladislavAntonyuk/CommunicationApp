namespace CommunicationApi.Services.Slack;

public interface ISlackService
{
    Task Send(string recipient, string text);
}
