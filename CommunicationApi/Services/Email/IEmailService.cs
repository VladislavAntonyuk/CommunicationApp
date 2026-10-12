namespace CommunicationApi.Services.Email;

public interface IEmailService
{
    Task<string?> Send(string recipient, string subject, string text);
}
