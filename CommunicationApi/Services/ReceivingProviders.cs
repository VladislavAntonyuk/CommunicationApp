using CommunicationApi.Services.Slack;
using CommunicationApi.Services.Telegram;
using Shared;

namespace CommunicationApi.Services;

public static class ReceivingProviders
{
    public static readonly Target[] All = [Target.Telegram, Target.Slack];

    public static bool IsConfigured(Target target, IConfiguration configuration)
    {
        switch (target)
        {
            case Target.Telegram:
                var telegram = configuration.GetSection("Telegram").Get<TelegramSettings>();
                return IsSet(telegram?.AppId) && telegram?.AppId != "0" && IsSet(telegram?.AppHash);
            case Target.Slack:
                var slack = configuration.GetSection("Slack").Get<SlackSettings>();
                return IsSet(slack?.AppLevelToken);
            default:
                return false;
        }
    }

    private static bool IsSet(string? value) => !string.IsNullOrWhiteSpace(value) && !value.StartsWith('<');
}
