using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Options;
using Shared;

namespace CommunicationApi.Services.Queue;

public class ServiceBusQueue(ServiceBusClient client, IOptions<MessageQueueSettings> options) : IMessageQueue
{
    private static readonly TimeSpan ReceiveWait = TimeSpan.FromSeconds(1);

    private readonly ServiceBusSender _sender = client.CreateSender(options.Value.Name);
    private readonly ServiceBusReceiver _receiver = client.CreateReceiver(options.Value.Name);
    private readonly ConcurrentDictionary<string, ServiceBusReceivedMessage> _unacknowledged = new();

    public async Task Enqueue(string text)
    {
        await _sender.SendMessageAsync(new ServiceBusMessage(text));
    }

    public async Task<List<ReceivedMessage>> Receive()
    {
        foreach (var id in _unacknowledged.Keys)
        {
            if (_unacknowledged.TryRemove(id, out var message))
                await Settle(_receiver.AbandonMessageAsync(message));
        }

        var messages = await _receiver.ReceiveMessagesAsync(options.Value.MaxMessages, ReceiveWait);
        var received = new List<ReceivedMessage>();
        foreach (var message in messages.OrderBy(x => x.SequenceNumber))
        {
            var id = message.SequenceNumber.ToString();
            _unacknowledged[id] = message;
            received.Add(new ReceivedMessage(id, message.Body.ToString()));
        }

        return received;
    }

    public async Task Acknowledge(IEnumerable<string> ids)
    {
        foreach (var id in ids)
        {
            if (_unacknowledged.TryRemove(id, out var message))
                await Settle(_receiver.CompleteMessageAsync(message));
        }
    }

    private static async Task Settle(Task settlement)
    {
        try
        {
            await settlement;
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageLockLost)
        {
        }
    }
}
