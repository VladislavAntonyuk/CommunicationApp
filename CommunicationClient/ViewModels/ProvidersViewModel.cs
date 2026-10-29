using CommunicationClient.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared;

namespace CommunicationClient.ViewModels;

public partial class ProvidersViewModel(ICommunicationApi api, INavigationService navigation, IDialogService dialogs) : ObservableObject
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
        Status = "Loading...";
        var response = await api.GetProviders();
        if (!response.IsSuccessfulWithContent)
        {
            Status = response.ErrorMessage;
            return;
        }

        var providers = response.Content;
        Providers = providers.Select(x => new ProviderRow(x, ToggleProvider)).ToList();
        Status = null;

        if (providers.Any(x => x is { Target: Target.Telegram, Configured: true }))
            await LoadTelegram();
    }

    [RelayCommand]
    private Task Login() => navigation.GoTo(nameof(TelegramLoginPage));

    [RelayCommand]
    private async Task Logout()
    {
        if (!await dialogs.Confirm("Telegram", "Log out of Telegram on the server?", "Log out", "Cancel"))
            return;

        var response = await api.TelegramLogout();
        if (!response.IsSuccessful)
        {
            TelegramAccount = response.ErrorMessage;
            return;
        }

        await LoadTelegram();
    }

    private async Task LoadTelegram()
    {
        ShowsTelegram = true;
        TelegramStep = null;
        HideChats();
        TelegramAccount = "Loading...";
        var response = await api.GetTelegramLogin();
        if (!response.IsSuccessfulWithContent)
        {
            TelegramAccount = response.ErrorMessage;
            return;
        }

        var status = response.Content;
        TelegramAccount = status.Step switch
        {
            TelegramLoginStep.LoggedIn => $"Connected as {status.Account}",
            TelegramLoginStep.Code or TelegramLoginStep.Password => "Login is not finished",
            _ => "Not connected"
        };
        TelegramStep = status.Step;

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
        ChatsStatus = "Loading...";
        var response = await api.GetChats(Target.Telegram);
        if (!response.IsSuccessfulWithContent)
        {
            ChatsStatus = response.ErrorMessage;
            return;
        }

        var chats = response.Content;
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

    private async Task ToggleProvider(ProviderRow row)
    {
        var enabled = row.Enabled = !row.Enabled;
        var response = await api.SetProvider(row.Target, new ProviderSwitch(enabled));
        if (response.IsSuccessful)
        {
            Status = null;
            return;
        }

        row.Enabled = !enabled;
        Status = response.ErrorMessage;
    }

    private async Task ToggleChat(ChatRow row)
    {
        var enabled = row.Enabled = !row.Enabled;
        var response = await api.SetChat(Target.Telegram, row.ChatId, new ProviderSwitch(enabled));
        if (response.IsSuccessful)
        {
            ChatsStatus = null;
            return;
        }

        row.Enabled = !enabled;
        ChatsStatus = response.ErrorMessage;
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
