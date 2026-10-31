namespace CommunicationApp.Services;

public interface ISmsSender
{
    void Send(string phoneNumber, string text);
}
