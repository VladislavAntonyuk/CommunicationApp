using System.Collections.Concurrent;
using CommunicationApi.Services.Auth;
using Shared.Api;
using TL;
using WTelegram;

namespace CommunicationApi.Services.Telegram;

public sealed class TelegramLogins(IConfiguration configuration, ILoggerFactory loggerFactory, IServiceScopeFactory scopeFactory) : IDisposable
{
    private readonly ConcurrentDictionary<int, TelegramLogin> _logins = new();
    private readonly ILogger<TelegramLogins> _logger = loggerFactory.CreateLogger<TelegramLogins>();

    public TelegramLogin For(int userId) =>
        _logins.GetOrAdd(userId, id => new TelegramLogin(id, configuration, loggerFactory.CreateLogger<TelegramLogin>(), (manager, update) => OnUpdate(id, manager, update)));

    public IEnumerable<int> SavedUserIds() =>
        Directory.EnumerateFiles(AppContext.BaseDirectory, "telegram-*.session")
            .Select(x => int.TryParse(Path.GetFileNameWithoutExtension(x)["telegram-".Length..], out var id) ? id : (int?)null)
            .OfType<int>();

    public void Dispose()
    {
        foreach (var login in _logins.Values)
            login.Close();
    }

    private async Task OnUpdate(int userId, UpdateManager manager, Update update)
    {
        if (update is not UpdateNewMessage { message: TL.Message message } || message.flags.HasFlag(TL.Message.Flags.out_))
            return;

        var kind = message.peer_id switch
        {
            PeerUser => ChatKind.Private,
            PeerChannel peer when manager.Chats.TryGetValue(peer.channel_id, out var chat) && chat.IsChannel => ChatKind.Channel,
            _ => ChatKind.Group
        };

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ICurrentUser>().Set(userId);
            await scope.ServiceProvider.GetRequiredService<OutgoingSmsSender>().Send(
                Target.Telegram,
                new Shared.Api.Message($"Telegram|{message.peer_id.ID}|{message.message}", message.peer_id.ID.ToString(), kind));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telegram message for user {UserId} was not forwarded", userId);
        }
    }
}
