using CommunicationClient.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared;

namespace CommunicationClient.ViewModels;

public partial class ProvidersViewModel(ApiClient api, INavigationService navigation, IDialogService dialogs) : ObservableObject
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
    [NotifyPropertyChangedFor(nameof(TelegramButtonText), nameof(ShowsTelegramButton))]
    public partial TelegramLoginStep? TelegramStep { get; set; }

    [ObservableProperty]
    public partial bool ShowsChats { get; set; }

    [ObservableProperty]
    public partial string? ChatsStatus { get; set; }

    [ObservableProperty]
    public partial List<ChatGroup>? ChatGroups { get; set; }

    public string TelegramButtonText => TelegramStep switch
    {
        TelegramLoginStep.LoggedIn => "Log out",
        TelegramLoginStep.Code or TelegramLoginStep.Password => "Continue login",
        _ => "Log in"
    };

    public bool ShowsTelegramButton => TelegramStep != null;

    [RelayCommand]
    private async Task Load()
    {
        try
        {
            if (!api.HasBackend)
            {
                Status = "Enter a valid backend URL on the main page";
                return;
            }

            Status = "Loading...";
            var providers = await api.GetProviders();
            Providers = providers.Select(x => new ProviderRow(x)).ToList();
            Status = null;

            if (providers.Any(x => x is { Target: Target.Telegram, Configured: true }))
                await LoadTelegram();
        }
        catch (Exception ex)
        {
            Status = ForwardingError.Describe(ex);
        }
    }

    [RelayCommand]
    private async Task LogInOrOut()
    {
        if (TelegramStep != TelegramLoginStep.LoggedIn)
        {
            await navigation.GoTo(nameof(TelegramLoginPage));
            return;
        }

        if (!await dialogs.Confirm("Telegram", "Log out of Telegram on the server?", "Log out", "Cancel"))
            return;

        try
        {
            await api.TelegramLogout();
            await LoadTelegram();
        }
        catch (Exception ex)
        {
            TelegramAccount = ForwardingError.Describe(ex);
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
            TelegramAccount = ForwardingError.Describe(ex);
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
                    new ChatGroup("Private chats", chats.Where(x => x.Kind == ChatKind.Private)),
                    new ChatGroup("Groups", chats.Where(x => x.Kind == ChatKind.Group)),
                    new ChatGroup("Channels", chats.Where(x => x.Kind == ChatKind.Channel))
                }
                .Where(x => x.Count > 0)
                .ToList();
            ChatsStatus = chats.Count == 0 ? "No chats" : null;
        }
        catch (Exception ex)
        {
            ChatsStatus = ForwardingError.Describe(ex);
        }
    }

    public async Task SetProvider(ProviderRow row, bool enabled)
    {
        if (enabled == row.Enabled)
            return;

        try
        {
            await api.SetProvider(row.Target, enabled);
            row.Enabled = enabled;
            Status = null;
        }
        catch (Exception ex)
        {
            row.ResetSwitch();
            Status = ForwardingError.Describe(ex);
        }
    }

    public async Task SetChat(ChatRow row, bool enabled)
    {
        // Also raised when a row is shown or reused for another chat while scrolling.
        if (enabled == row.Enabled)
            return;

        try
        {
            await api.SetChat(Target.Telegram, row.ChatId, enabled);
            row.Enabled = enabled;
            ChatsStatus = null;
        }
        catch (Exception ex)
        {
            row.ResetSwitch();
            ChatsStatus = ForwardingError.Describe(ex);
        }
    }
}

public class ProviderRow(ProviderStatus status) : ObservableObject
{
    public Target Target { get; } = status.Target;
    public string Title { get; } = status.Configured ? status.Target.ToString() : $"{status.Target} (not set up)";
    public bool Configured { get; } = status.Configured;
    public bool Enabled { get; set; } = status.Enabled;

    public void ResetSwitch() => OnPropertyChanged(nameof(Enabled));
}

public class ChatGroup(string title, IEnumerable<ChatStatus> chats) : List<ChatRow>(chats.Select(x => new ChatRow(x)))
{
    public string Title { get; } = title;
}

public class ChatRow(ChatStatus status) : ObservableObject
{
    public string ChatId { get; } = status.ChatId;
    public string Title { get; } = status.Title;
    public bool Enabled { get; set; } = status.Enabled;

    public void ResetSwitch() => OnPropertyChanged(nameof(Enabled));
}
