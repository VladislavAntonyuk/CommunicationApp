using CommunicationApi.Services.Auth;
using CommunicationApi.Services.Email;
using CommunicationApi.Services.Slack;
using CommunicationApi.Services.Teams;
using CommunicationApi.Services.Telegram;
using Shared.Api;

namespace CommunicationApp.Tests.Helpers;

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

    public List<int?> TelegramUsers { get; } = [];

    public ITelegramService ForUser(ICurrentUser user) => new UserTelegram(this, user);

    public int Attempts { get; private set; }

    private Task Record(SentMessage message)
    {
        Attempts++;
        if (Exception != null)
            throw Exception;

        Sent.Add(message);
        return Task.CompletedTask;
    }

    private sealed class UserTelegram(FakeSenders senders, ICurrentUser user) : ITelegramService
    {
        public async Task Send(string recipient, string text)
        {
            await ((ITelegramService)senders).Send(recipient, text);
            senders.TelegramUsers.Add(user.Id);
        }

        public Task<List<TelegramChat>> GetChats() => ((ITelegramService)senders).GetChats();
    }
}
