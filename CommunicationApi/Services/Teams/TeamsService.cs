using Azure.Identity;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using CommunicationApi.Services;

namespace CommunicationApi.Services.Teams;

public class TeamsService(IConfiguration configuration) : ITeamsService
{
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(30);

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

        using var timeout = new CancellationTokenSource(SendTimeout);
        try
        {
            await graphClient.Chats[recipient].Messages.PostAsync(new ChatMessage
            {
                Body = new ItemBody
                {
                    Content = text
                }
            }, cancellationToken: timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            throw new PermanentSendException($"Teams did not respond in {SendTimeout.TotalSeconds:F0} s, sign-in may be required");
        }
    }
}