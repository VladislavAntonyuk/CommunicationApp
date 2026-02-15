using Mailjet.Client;
using Mailjet.Client.TransactionalEmails;
using Microsoft.Extensions.Options;

namespace CommunicationApi.Services;

internal class EmailService(IOptions<EmailSettings> options)
{
    public async Task<string?> Send(string recipient, string subject, string text)
    {
        MailjetClient client = new MailjetClient(options.Value.ApiKey, options.Value.ApiSecret);

        var email = new TransactionalEmailBuilder()
            .WithFrom(new SendContact(options.Value.FromEmail))
            .WithSubject(subject)
            .WithTextPart(text)
            .WithTo(new SendContact(recipient))
            .Build();

        var response = await client.SendTransactionalEmailAsync(email);

        return response.Messages.Length > 0 ? response.Messages[0].Status : "Email is not sent";
    }
}


public class EmailSettings
{
    public required string ApiKey { get; set; }
    public required string ApiSecret { get; set; }
    public required string FromEmail { get; set; }
}