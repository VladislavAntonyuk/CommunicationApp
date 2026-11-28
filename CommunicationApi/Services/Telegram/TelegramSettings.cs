namespace CommunicationApi.Services.Telegram;

public class TelegramSettings
{
    public required string AppId { get; set; }
    public required string AppHash { get; set; }
    public TimeSpan MaxMessageAge { get; set; } = TimeSpan.FromMinutes(5);
}
