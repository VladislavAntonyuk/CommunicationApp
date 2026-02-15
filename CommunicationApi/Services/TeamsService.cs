using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Authentication;
using Microsoft.Graph.Models;
using Microsoft.Identity.Client;

namespace CommunicationApi.Services;

public class TeamsService(IPublicClientApplication pca)
{
    public async Task Send(string recipient, string text)
    {
        var clientId = "YOUR_APP_CLIENT_ID";
        var scopes = new[] { "User.Read", "Chat.Read" };

        // Device code credential
        var deviceCodeCredential = new DeviceCodeCredential(
            (deviceCodeInfo, cancellationToken) =>
            {
                Console.WriteLine(deviceCodeInfo.Message); // Show code to user
                return Task.CompletedTask;
            },
            clientId: clientId,
            tenantId: "common" // 'common' allows personal Microsoft accounts
        );

        // Graph client using TokenCredentialAuthProvider
        var graphClient = new GraphServiceClient(new AzureIdentityAuthenticationProvider(deviceCodeCredential, null, null, true, scopes));
        
        await graphClient.Chats[recipient].Messages.PostAsync(new ChatMessage
        {
            Body = new ItemBody
            {
                Content = text
            }
        });
    }
}