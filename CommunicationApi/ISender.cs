namespace CommunicationApi;

internal interface ISender
{
    Task Send(string recipient, string text);
}