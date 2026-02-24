using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace CommunicationApi.Services.Teams;

public class TeamsService(IConfiguration configuration)
{
    public async Task Send(string recipient, string text)
    {
        var clientId = configuration["Teams:ClientId"];
        var scopes = new[] { "User.Read", "Chat.Read", "Chat.ReadWrite", "ChatMessage.Send", "offline_access" };

        var options = new DeviceCodeCredentialOptions
        {
            AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
            ClientId = clientId,
            TenantId = "common",
            DeviceCodeCallback = (code, token) =>
            {
                Console.WriteLine(code.Message);
                return Task.CompletedTask;
            }
        };

        var deviceCodeCredential = new DeviceCodeCredential(options);

        var graphClient = new GraphServiceClient(deviceCodeCredential, scopes);

        var chats = await graphClient.Chats.GetAsync();

        foreach (var chat in chats.Value)
        {
            Console.WriteLine($"Chat: {chat.Topic}");

            var messages = await graphClient.Chats[chat.Id].Messages.GetAsync();
            foreach (var msg in messages.Value)
            {
                Console.WriteLine($"{msg.From?.User?.DisplayName}: {msg.Body?.Content}");
            }
        }

        await graphClient.Chats[recipient].Messages.PostAsync(new ChatMessage
        {
            Body = new ItemBody
            {
                Content = text
            }
        });
    }
}