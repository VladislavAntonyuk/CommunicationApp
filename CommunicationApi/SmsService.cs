using Microsoft.Extensions.Options;

namespace CommunicationApi;

internal class SmsService(IOptions<Recipient> options):ISender
{
    public Task Send(string recipient, string text)
    {
        var recipient = options.Value.Sms;
        return Task.CompletedTask;
    }
}