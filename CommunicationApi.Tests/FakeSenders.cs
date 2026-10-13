using CommunicationApi.Services.Email;
using CommunicationApi.Services.Slack;
using CommunicationApi.Services.Teams;
using CommunicationApi.Services.Telegram;
using Shared;

namespace CommunicationApi.Tests;

public record SentMessage(Target Target, string Recipient, string? Subject, string Text);

public sealed class FakeSenders : IEmailService, ITelegramService, ITeamsService, ISlackService
{
    public List<SentMessage> Sent { get; } = [];

    public string? EmailStatus { get; set; } = "success";

    public Exception? Exception { get; set; }

    Task<string?> IEmailService.Send(string recipient, string subject, string text)
    {
        Record(new SentMessage(Target.Email, recipient, subject, text));
        return Task.FromResult(EmailStatus);
    }

    Task ITelegramService.Send(string recipient, string text) => Record(new SentMessage(Target.Telegram, recipient, null, text));

    public List<TelegramChat> TelegramChats { get; } = [];

    Task<List<TelegramChat>> ITelegramService.GetChats()
    {
        if (Exception != null)
            throw Exception;

        return Task.FromResult(TelegramChats);
    }

    Task ITeamsService.Send(string recipient, string text) => Record(new SentMessage(Target.Teams, recipient, null, text));

    Task ISlackService.Send(string recipient, string text) => Record(new SentMessage(Target.Slack, recipient, null, text));

    private Task Record(SentMessage message)
    {
        if (Exception != null)
            throw Exception;

        Sent.Add(message);
        return Task.CompletedTask;
    }
}
