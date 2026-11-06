using System.Text;
using CommunicationApi.Database;
using Microsoft.Extensions.Options;
using Silverback.Messaging.Consuming.ErrorHandling;
using Silverback.Messaging.Messages;

namespace CommunicationApi.Services.Messaging;

public class IncomingSmsSkipRecorder(CommunicationApiDbContext dbContext, IOptions<MqttSettings> options) : ISkipMessageErrorPolicyCallback
{
    public async Task OnSkippingMessageAsync(IRawInboundEnvelope envelope, Exception exception)
    {
        if (envelope.Endpoint.RawName != options.Value.IncomingTopic ||
            envelope.Headers.GetValue(MqttMessageHeaders.CorrelationData) is not { } correlationData)
            return;

        var id = Encoding.UTF8.GetString(Convert.FromBase64String(correlationData));
        if (await dbContext.IncomingMessages.FindAsync(id) != null)
            return;

        dbContext.IncomingMessages.Add(new IncomingMessage { Id = id, ReceivedAt = DateTime.UtcNow, Error = exception.Message });
        await dbContext.SaveChangesAsync();
    }
}
