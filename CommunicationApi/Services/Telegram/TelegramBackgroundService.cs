namespace CommunicationApi.Services.Telegram;

public class TelegramBackgroundService(TelegramLogins logins, ILogger<TelegramBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        foreach (var userId in logins.SavedUserIds())
        {
            try
            {
                await logins.For(userId).GetStatus();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not continue the saved Telegram session of user {UserId}", userId);
            }
        }
    }
}
