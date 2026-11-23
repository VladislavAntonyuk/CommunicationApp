using CommunicationApi.Database;
using Microsoft.Extensions.Options;
using Shared;
using Silverback.Messaging.Messages;

namespace CommunicationApi.Services.Messaging;

public class IncomingSmsSubscriber(
    CommunicationApiDbContext dbContext,
    IncomingSmsRouter router,
    IOptions<ClientSettings> client,
    ILogger<IncomingSmsSubscriber> logger)
{
    public async Task HandleAsync(IInboundEnvelope<IncomingSms> envelope)
    {
        if (envelope.GetMqttCorrelationDataAsString() is not { Length: > 0 } id || envelope.Message is not { } sms)
        {
            logger.LogWarning("Incoming SMS without an id was ignored");
            return;
        }

        if (!PhoneNumberMatcher.IsSameNumber(sms.From, client.Value.PhoneNumber))
        {
            logger.LogWarning("Incoming SMS {Id} from an unknown number was ignored", id);
            return;
        }

        if (await dbContext.IncomingMessages.FindAsync(id) != null)
            return;

        var message = new IncomingMessage { Id = id, ReceivedAt = DateTime.UtcNow };
        try
        {
            await router.Send(sms.Text);
            message.Sent = true;
        }
        catch (PermanentSendException ex)
        {
            message.Error = ex.Message;
        }

        dbContext.IncomingMessages.Add(message);
        await dbContext.SaveChangesAsync();
    }
}
