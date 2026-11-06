using CommunicationApi.Services.Email;
using CommunicationApi.Services.Slack;
using CommunicationApi.Services.Teams;
using CommunicationApi.Services.Telegram;
using Shared;
using SlackNet;

namespace CommunicationApi.Services;

public sealed class IncomingSmsRouter(
    IEmailService email,
    ITelegramService telegram,
    ITeamsService teams,
    ISlackService slack)
{
    public async Task Send(string? text)
    {
        var (target, parts) = Parse(text);
        try
        {
            switch (target)
            {
                case Target.Email:
                    var status = await email.Send(parts[1], parts[2], parts[3]);
                    if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
                        throw new PermanentSendException($"Email is not sent: {status}");
                    break;
                case Target.Telegram:
                    await telegram.Send(parts[1], parts[2]);
                    break;
                case Target.Teams:
                    await teams.Send(parts[1], parts[2]);
                    break;
                case Target.Slack:
                    await slack.Send(parts[1], parts[2]);
                    break;
            }
        }
        catch (Exception ex) when (ex is TelegramLoginException or SlackException or ArgumentException)
        {
            throw new PermanentSendException(ex.Message, ex);
        }
    }

    private static (Target Target, string[] Parts) Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidSmsException("Text is required");

        var targetName = text.Split('|', 2)[0].Trim();
        if (!Enum.GetNames<Target>().Contains(targetName, StringComparer.OrdinalIgnoreCase))
            throw new InvalidSmsException($"Unknown target '{targetName}'");

        var target = Enum.Parse<Target>(targetName, true);
        var fieldCount = target == Target.Email ? 4 : 3;
        var parts = text.Split('|', fieldCount);
        if (parts.Length < fieldCount)
            throw new InvalidSmsException(target == Target.Email ? "Expected Email|to|subject|text" : $"Expected {target}|recipient|text");

        if (target == Target.Telegram && !long.TryParse(parts[1], out _))
            throw new InvalidSmsException("Telegram recipient must be a numeric chat id");

        return (target, parts);
    }
}
