using MailerSendNetCore.Common.Interfaces;
using MailerSendNetCore.Emails.Dtos;
using Microsoft.Extensions.Options;

namespace CommunicationApi;

internal class EmailService(IMailerSendEmailClient client, IOptions<EmailSettings> options)
{
    public async Task Send(string recipient, string subject, string text)
    {
        var parameters = new MailerSendEmailParameters
        {
            Text = text
        };
        parameters
            .WithSubject(subject)
            .WithFrom(options.Value.FromEmail, options.Value.FromName)
            .WithTo(recipient);

        await client.SendEmailAsync(parameters);
    }
}


public class EmailSettings
{
    public required string FromEmail { get; set; }
    public required string FromName { get; set; }
}