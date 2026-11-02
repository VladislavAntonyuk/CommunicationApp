namespace CommunicationApi.Services.Messaging;

public interface IMessagePublisher
{
    Task Publish(string text);
}
