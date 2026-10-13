using Shared;
using TL;
using WTelegram;

namespace CommunicationApi.Services.Telegram;

public sealed class TelegramLogin(IConfiguration configuration, ILogger<TelegramLogin> logger) : ITelegramLogin, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _state = new();
    private Client? _client;
    private TelegramLoginStep _step = TelegramLoginStep.Phone;
    private bool _resumed;
    private string? _phone;
    // The value the Telegram library is waiting for.
    private TaskCompletionSource<string>? _pending;
    // Completed when the library asks for the next value, or the login ends.
    private TaskCompletionSource _next = new();
    private Task _login = Task.CompletedTask;

    public Client? Client
    {
        get
        {
            lock (_state)
            {
                if (_client == null && ReceivingProviders.IsConfigured(Target.Telegram, configuration))
                    _client = new Client(Config);

                return _client;
            }
        }
    }

    public async Task<TelegramLoginStatus> GetStatus()
    {
        await _gate.WaitAsync();
        try
        {
            await Resume();
            return Status();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TelegramLoginStatus> Submit(string value)
    {
        await _gate.WaitAsync();
        try
        {
            await Resume();
            var step = _step;
            switch (step)
            {
                case TelegramLoginStep.NotConfigured:
                    throw new TelegramLoginException("Telegram is not set up on the server");
                case TelegramLoginStep.LoggedIn:
                    throw new TelegramLoginException("Telegram is already logged in");
                case TelegramLoginStep.Phone:
                    lock (_state)
                    {
                        _phone = value.Trim();
                        _next = new TaskCompletionSource();
                    }
                    _login = Task.Run(Login);
                    break;
                default:
                    TaskCompletionSource<string>? pending;
                    lock (_state)
                    {
                        pending = _pending;
                        _pending = null;
                        _next = new TaskCompletionSource();
                    }
                    pending?.TrySetResult(value.Trim());
                    break;
            }

            try
            {
                await _next.Task;
            }
            catch (Exception ex) when (ex is not TelegramLoginException)
            {
                throw Describe(ex);
            }

            if (_step == step)
                throw new TelegramLoginException(step == TelegramLoginStep.Code ? "Wrong code" : "Wrong password");

            return Status();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TelegramLoginStatus> Logout()
    {
        await _gate.WaitAsync();
        try
        {
            await Resume();
            if (_step == TelegramLoginStep.LoggedIn)
            {
                try
                {
                    await _client!.Auth_LogOut();
                }
                finally
                {
                    Forget();
                }
            }
            else
            {
                TaskCompletionSource<string>? pending;
                lock (_state)
                {
                    pending = _pending;
                    _pending = null;
                }
                pending?.TrySetCanceled();
                await _login;
            }

            return Status();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Client> GetLoggedInClient()
    {
        var status = await GetStatus();
        if (status.Step != TelegramLoginStep.LoggedIn)
            throw new TelegramLoginException("Telegram is not logged in");

        return _client!;
    }

    public void Dispose()
    {
        _pending?.TrySetCanceled();
        _client?.Dispose();
        _gate.Dispose();
    }

        private async Task Resume()
    {
        if (_resumed)
            return;

        var client = Client;
        if (client == null)
        {
            _step = TelegramLoginStep.NotConfigured;
            return;
        }

        if (client.UserId != 0)
        {
            try
            {
                await client.LoginUserIfNeeded(reloginOnFailedResume: false);
                _step = TelegramLoginStep.LoggedIn;
            }
            catch (RpcException ex)
            {
                logger.LogWarning(ex, "Saved Telegram session cannot be continued");
                Forget();
            }
        }

        _resumed = true;
    }

    private async Task Login()
    {
        try
        {
            await _client!.LoginUserIfNeeded();
            lock (_state)
            {
                _step = TelegramLoginStep.LoggedIn;
                _phone = null;
            }
            _next.TrySetResult();
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
                logger.LogWarning(ex, "Telegram login failed");
            Forget();
            _next.TrySetException(ex);
        }
    }

    private void Forget()
    {
        try
        {
            _client?.Reset();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Telegram client reset failed");
        }

        lock (_state)
        {
            _step = TelegramLoginStep.Phone;
            _phone = null;
            _pending = null;
        }
    }

    private TelegramLoginStatus Status()
    {
        var phone = _step == TelegramLoginStep.LoggedIn ? _client?.User?.phone : null;
        return new TelegramLoginStatus(_step, string.IsNullOrEmpty(phone) ? null : Mask(phone));
    }

    private static string Mask(string phone)
    {
        return phone.Length <= 5
            ? $"+{phone[..1]}{new string('•', phone.Length - 1)}"
            : $"+{phone[..3]}{new string('•', phone.Length - 5)}{phone[^2..]}";
    }

    private string? Config(string what)
    {
        var settings = configuration.GetSection("Telegram").Get<TelegramSettings>();
        switch (what)
        {
            case "api_id":
                return settings?.AppId;
            case "api_hash":
                return settings?.AppHash;
            case "session_pathname":
                return Path.Combine(AppContext.BaseDirectory, "telegram.session");
            case "user_id":
                // Accept whichever account the saved session belongs to.
                return "-1";
            case "phone_number":
                return _phone;
            case "verification_code":
                return Ask(TelegramLoginStep.Code);
            case "password":
                return Ask(TelegramLoginStep.Password);
            case "first_name":
            case "last_name":
                throw new TelegramLoginException("There is no Telegram account with this phone number");
            case "email_verification_code":
                throw new TelegramLoginException("Telegram asks to confirm an email first, log in with the Telegram app to do that");
            default:
                return null;
        }
    }

    // Blocks the library until Submit brings the value.
    private string Ask(TelegramLoginStep step)
    {
        TaskCompletionSource<string> pending;
        lock (_state)
        {
            pending = _pending = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _step = step;
        }
        _next.TrySetResult();
        return pending.Task.GetAwaiter().GetResult();
    }

    private static Exception Describe(Exception ex) => ex switch
    {
        OperationCanceledException => new TelegramLoginException("Login was cancelled"),
        RpcException { Message: "PHONE_NUMBER_INVALID" } => new TelegramLoginException("Invalid phone number"),
        RpcException { Message: "PHONE_NUMBER_BANNED" } => new TelegramLoginException("This phone number is banned by Telegram"),
        RpcException { Message: "PHONE_CODE_INVALID" } => new TelegramLoginException("Wrong code too many times, start again"),
        RpcException { Message: "PHONE_CODE_EXPIRED" } => new TelegramLoginException("The code has expired, start again"),
        RpcException { Message: "PASSWORD_HASH_INVALID" } => new TelegramLoginException("Wrong password too many times, start again"),
        RpcException { Message: "FLOOD_WAIT_X", X: > 0 } flood => new TelegramLoginException($"Too many attempts, try again in {flood.X} seconds"),
        _ => ex
    };
}
