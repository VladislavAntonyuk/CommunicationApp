using CommunicationClient.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared;

namespace CommunicationClient.ViewModels;

public partial class ProvidersViewModel(ICommunicationApi api, IPreferences preferences, INavigationService navigation, IDialogService dialogs) : ObservableObject
{
    [ObservableProperty]
    public partial string? Status { get; set; }

    [ObservableProperty]
    public partial List<ProviderRow> Providers { get; set; } = [];

    [ObservableProperty]
    public partial bool ShowsTelegram { get; set; }

    [ObservableProperty]
    public partial string? TelegramAccount { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LoginText), nameof(ShowsLogin), nameof(ShowsLogout))]
    public partial TelegramLoginStep? TelegramStep { get; set; }

    [ObservableProperty]
    public partial bool ShowsChats { get; set; }

    [ObservableProperty]
    public partial string? ChatsStatus { get; set; }

    [ObservableProperty]
    public partial List<ChatGroup>? ChatGroups { get; set; }

    public string LoginText => TelegramStep is TelegramLoginStep.Code or TelegramLoginStep.Password ? "Continue login" : "Log in";

    public bool ShowsLogin => TelegramStep is not null and not TelegramLoginStep.LoggedIn;

    public bool ShowsLogout => TelegramStep == TelegramLoginStep.LoggedIn;

    [RelayCommand]
    private async Task Load()
    {
        try
        {
            if (BackendAddress.From(preferences) is null)
            {
                Status = "Enter a valid backend URL on the main page";
                return;
            }

            Status = "Loading...";
            var providers = await api.GetProviders();
            Providers = providers.Select(x => new ProviderRow(x, ToggleProvider)).ToList();
            Status = null;

            if (providers.Any(x => x is { Target: Target.Telegram, Configured: true }))
                await LoadTelegram();
        }
        catch (Exception ex)
        {
            Status = ApiError.Describe(ex);
        }
    }

    [RelayCommand]
    private Task Login() => navigation.GoTo(nameof(TelegramLoginPage));

    [RelayCommand]
    private async Task Logout()
    {
        if (!await dialogs.Confirm("Telegram", "Log out of Telegram on the server?", "Log out", "Cancel"))
            return;

        try
        {
            await api.TelegramLogout();
            await LoadTelegram();
        }
        catch (Exception ex)
        {
            TelegramAccount = ApiError.Describe(ex);
        }
    }

    private async Task LoadTelegram()
    {
        ShowsTelegram = true;
        TelegramStep = null;
        HideChats();
        try
        {
            TelegramAccount = "Loading...";
            var status = await api.GetTelegramLogin();
            TelegramAccount = status?.Step switch
            {
                TelegramLoginStep.LoggedIn => $"Connected as {status.Account}",
                TelegramLoginStep.Code or TelegramLoginStep.Password => "Login is not finished",
                _ => "Not connected"
            };
            TelegramStep = status?.Step ?? TelegramLoginStep.NotConfigured;
        }
        catch (Exception ex)
        {
            TelegramAccount = ApiError.Describe(ex);
            return;
        }

        if (TelegramStep == TelegramLoginStep.LoggedIn)
            await LoadChats();
    }

    private void HideChats()
    {
        ChatGroups = null;
        ShowsChats = false;
        ChatsStatus = null;
    }

    private async Task LoadChats()
    {
        ShowsChats = true;
        try
        {
            ChatsStatus = "Loading...";
            var chats = await api.GetChats(Target.Telegram);
            ChatGroups = new[]
                {
                    new ChatGroup("Private chats", chats.Where(x => x.Kind == ChatKind.Private), ToggleChat),
                    new ChatGroup("Groups", chats.Where(x => x.Kind == ChatKind.Group), ToggleChat),
                    new ChatGroup("Channels", chats.Where(x => x.Kind == ChatKind.Channel), ToggleChat)
                }
                .Where(x => x.Count > 0)
                .ToList();
            ChatsStatus = chats.Count == 0 ? "No chats" : null;
        }
        catch (Exception ex)
        {
            ChatsStatus = ApiError.Describe(ex);
        }
    }

    private async Task ToggleProvider(ProviderRow row)
    {
        var enabled = row.Enabled = !row.Enabled;
        try
        {
            await api.SetProvider(row.Target, new ProviderSwitch(enabled));
            Status = null;
        }
        catch (Exception ex)
        {
            row.Enabled = !enabled;
            Status = ApiError.Describe(ex);
        }
    }

    private async Task ToggleChat(ChatRow row)
    {
        var enabled = row.Enabled = !row.Enabled;
        try
        {
            await api.SetChat(Target.Telegram, row.ChatId, new ProviderSwitch(enabled));
            ChatsStatus = null;
        }
        catch (Exception ex)
        {
            row.Enabled = !enabled;
            ChatsStatus = ApiError.Describe(ex);
        }
    }
}

public partial class ProviderRow(ProviderStatus status, Func<ProviderRow, Task> toggle) : ObservableObject
{
    public Target Target { get; } = status.Target;
    public string Title { get; } = status.Configured ? status.Target.ToString() : $"{status.Target} (not set up)";
    public bool Configured { get; } = status.Configured;

    [ObservableProperty]
    public partial bool Enabled { get; set; } = status.Enabled;

    [RelayCommand(CanExecute = nameof(Configured))]
    private Task Toggle() => toggle(this);
}

public class ChatGroup(string title, IEnumerable<ChatStatus> chats, Func<ChatRow, Task> toggle)
    : List<ChatRow>(chats.Select(x => new ChatRow(x, toggle)))
{
    public string Title { get; } = title;
}

public partial class ChatRow(ChatStatus status, Func<ChatRow, Task> toggle) : ObservableObject
{
    public string ChatId { get; } = status.ChatId;
    public string Title { get; } = status.Title;

    [ObservableProperty]
    public partial bool Enabled { get; set; } = status.Enabled;

    [RelayCommand]
    private Task Toggle() => toggle(this);
}
