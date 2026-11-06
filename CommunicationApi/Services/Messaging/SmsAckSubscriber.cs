using CommunicationApi.Database;
using Shared;
using Silverback.Messaging.Messages;

namespace CommunicationApi.Services.Messaging;

public class SmsAckSubscriber(CommunicationApiDbContext dbContext)
{
    public async Task HandleAsync(IInboundEnvelope<SmsAck> envelope)
    {
        if (envelope.GetMqttCorrelationDataAsString() is not { } id || envelope.Message is not { } ack)
            return;

        var message = await dbContext.OutgoingMessages.FindAsync(id);
        if (message is not { Sent: null })
            return;

        message.Sent = ack.Sent;
        message.Error = ack.Error;
        await dbContext.SaveChangesAsync();
    }
}
