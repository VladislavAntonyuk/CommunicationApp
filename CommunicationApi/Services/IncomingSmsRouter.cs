using CommunicationApi.Services.Telegram;
using Shared.Api;

namespace CommunicationApi.Services;

public sealed class IncomingSmsRouter(ITelegramService telegram)
{
    public async Task Send(string? text)
    {
        var parts = Parse(text);
        try
        {
            await telegram.Send(parts[1], parts[2]);
        }
        catch (Exception ex) when (ex is TelegramLoginException or ArgumentException)
        {
            throw new PermanentSendException(ex.Message, ex);
        }
    }

    private static string[] Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new PermanentSendException("Text is required");

        var targetName = text.Split('|', 2)[0].Trim();
        if (!Enum.GetNames<Target>().Contains(targetName, StringComparer.OrdinalIgnoreCase))
            throw new PermanentSendException($"Unknown target '{targetName}'");

        var target = Enum.Parse<Target>(targetName, true);
        if (target != Target.Telegram)
            throw new PermanentSendException("Only Telegram is supported for now");

        var parts = text.Split('|', 3);
        if (parts.Length < 3)
            throw new PermanentSendException("Expected Telegram|recipient|text");

        if (!long.TryParse(parts[1], out _))
            throw new PermanentSendException("Telegram recipient must be a numeric chat id");

        return parts;
    }
}
