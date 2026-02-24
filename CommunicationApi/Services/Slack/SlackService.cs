using Microsoft.Extensions.Options;
using SlackNet;
using SlackNet.WebApi;

namespace CommunicationApi.Services.Slack;

public class SlackService(IOptions<SlackSettings> settings)
{
    public async Task Send(string recipient, string text)
    {
        var slackServices = new SlackServiceBuilder()
            .UseApiToken(settings.Value.ApiToken);

        var client = slackServices.GetApiClient();
        await client.Chat.PostMessage(new Message
        {
            Text = text,
            AsUser = true,
            
            Channel = recipient
        });
    }
}