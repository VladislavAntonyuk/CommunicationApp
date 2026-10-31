using Azure;
using Azure.Storage.Queues;
using Shared;

namespace CommunicationApi.Services.Queue;

public class StorageQueue(QueueClient queue, TimeSpan leaseTime) : IMessageQueue
{
    private const int MaxMessages = 32;

    public async Task Enqueue(string text)
    {
        await queue.SendMessageAsync(text);
    }

    public async Task<List<ReceivedMessage>> Receive()
    {
        var messages = await queue.ReceiveMessagesAsync(MaxMessages, leaseTime);
        return messages.Value.Select(x => new ReceivedMessage($"{x.MessageId}|{x.PopReceipt}", x.MessageText)).ToList();
    }

    public async Task Acknowledge(IEnumerable<string> ids)
    {
        foreach (var id in ids)
        {
            var parts = id.Split('|', 2);
            if (parts.Length != 2)
                continue;

            try
            {
                await queue.DeleteMessageAsync(parts[0], parts[1]);
            }
            catch (RequestFailedException ex) when (ex.Status is 400 or 404)
            {
            }
        }
    }
}
