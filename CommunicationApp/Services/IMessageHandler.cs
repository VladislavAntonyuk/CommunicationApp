namespace CommunicationApp.Services;

public interface IMessageHandler
{
    Task Handle(string text);

    void Connected();

    void Failed(string reason);
}
