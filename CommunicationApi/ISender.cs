namespace CommunicationApi;

internal interface ISender
{
    Task Send(string text);
}