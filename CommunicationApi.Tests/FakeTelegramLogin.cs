using CommunicationApi.Services.Telegram;
using Shared;

namespace CommunicationApi.Tests;

public sealed class FakeTelegramLogin : ITelegramLogin
{
    public TelegramLoginStatus Status { get; set; } = new(TelegramLoginStep.Phone);

    public List<string> Submitted { get; } = [];

    public int Logouts { get; private set; }

    public Exception? Exception { get; set; }

    public Task<TelegramLoginStatus> GetStatus() => Result();

    public Task<TelegramLoginStatus> Submit(string value)
    {
        Submitted.Add(value);
        return Result();
    }

    public Task<TelegramLoginStatus> Logout()
    {
        Logouts++;
        return Result();
    }

    private Task<TelegramLoginStatus> Result()
    {
        if (Exception != null)
            throw Exception;

        return Task.FromResult(Status);
    }
}
