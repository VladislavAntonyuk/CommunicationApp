using Shared;

namespace CommunicationApi.Services.Queue;

public interface IMessageQueue
{
    Task Enqueue(string text);
    Task<List<ReceivedMessage>> Receive();
    Task Acknowledge(IEnumerable<string> ids);
}
