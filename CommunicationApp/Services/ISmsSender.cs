namespace CommunicationApp.Services;

public interface ISmsSender
{
    Task Send(string phoneNumber, string text);
}
