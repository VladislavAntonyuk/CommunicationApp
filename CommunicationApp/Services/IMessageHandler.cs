namespace CommunicationApp.Services;

public interface IMessageHandler
{
    Task Handle(string to, string text);

    void Connected();

    void Failed(string reason);
}
