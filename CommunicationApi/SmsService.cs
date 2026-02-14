using Microsoft.Extensions.Options;

namespace CommunicationApi;

internal class SmsService
{
    public Task Send(string recipient, string text)
    {
        return Task.CompletedTask;
    }
}