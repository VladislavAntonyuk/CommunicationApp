using System.ComponentModel;
using System.Net.Http.Json;
using Shared;

namespace CommunicationApp;

public partial class ProvidersPage : ContentPage
{
    private bool _telegramLoggedIn;

    public ProvidersPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            var backend = Preferences.Get("backend", "");
            if (!Uri.TryCreate(backend, UriKind.Absolute, out _))
            {
                ShowStatus("Enter a valid backend URL on the main page");
                return;
            }

            ShowStatus("Loading...");
            using var httpClient = new HttpClient();
            var providers = await httpClient.GetFromJsonAsync<List<ProviderStatus>>($"{backend}/providers") ?? [];
            BindableLayout.SetItemsSource(Providers, providers.Select(x => new ProviderRow(x)).ToList());
            ShowStatus(null);

            if (providers.Any(x => x is { Target: Target.Telegram, Configured: true }))
                await LoadTelegram(httpClient, backend);
        }
        catch (Exception ex)
        {
            ShowStatus(ForwardingError.Describe(ex));
        }
    }

    private async Task LoadTelegram(HttpClient httpClient, string backend)
    {
        TelegramSection.IsVisible = true;
        TelegramButton.IsVisible = false;
        HideChats();
        try
        {
            TelegramAccount.Text = "Loading...";
            var response = await httpClient.GetAsync($"{backend}/telegram/login");
            if (!response.IsSuccessStatusCode)
            {
                TelegramAccount.Text = await ServerError.Read(response);
                return;
            }

            var status = await response.Content.ReadFromJsonAsync<TelegramLoginStatus>();
            _telegramLoggedIn = status?.Step == TelegramLoginStep.LoggedIn;
            TelegramAccount.Text = status?.Step switch
            {
                TelegramLoginStep.LoggedIn => $"Connected as {status.Account}",
                TelegramLoginStep.Code or TelegramLoginStep.Password => "Login is not finished",
                _ => "Not connected"
            };
            TelegramButton.Text = status?.Step switch
            {
                TelegramLoginStep.LoggedIn => "Log out",
                TelegramLoginStep.Code or TelegramLoginStep.Password => "Continue login",
                _ => "Log in"
            };
            TelegramButton.IsVisible = true;
        }
        catch (Exception ex)
        {
            TelegramAccount.Text = ForwardingError.Describe(ex);
            return;
        }

        if (_telegramLoggedIn)
            await LoadChats(httpClient, backend);
    }

    private async void TelegramButton_OnClicked(object? sender, EventArgs e)
    {
        if (!_telegramLoggedIn)
        {
            await Shell.Current.GoToAsync(nameof(TelegramLoginPage));
            return;
        }

        if (!await DisplayAlertAsync("Telegram", "Log out of Telegram on the server?", "Log out", "Cancel"))
            return;

        var backend = Preferences.Get("backend", "");
        TelegramButton.IsEnabled = false;
        try
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.PostAsync($"{backend}/telegram/logout", null);
            if (response.IsSuccessStatusCode)
                await LoadTelegram(httpClient, backend);
            else
                TelegramAccount.Text = await ServerError.Read(response);
        }
        catch (Exception ex)
        {
            TelegramAccount.Text = ForwardingError.Describe(ex);
        }
        finally
        {
            TelegramButton.IsEnabled = true;
        }
    }

    private void HideChats()
    {
        Chats.ItemsSource = null;
        ChatsTitle.IsVisible = false;
        ShowChatsStatus(null);
    }

    private async Task LoadChats(HttpClient httpClient, string backend)
    {
        ChatsTitle.IsVisible = true;
        try
        {
            ShowChatsStatus("Loading...");
            var response = await httpClient.GetAsync($"{backend}/providers/{Target.Telegram}/chats");
            if (!response.IsSuccessStatusCode)
            {
                ShowChatsStatus(await ServerError.Read(response));
                return;
            }

            var chats = await response.Content.ReadFromJsonAsync<List<ChatStatus>>() ?? [];
            Chats.ItemsSource = new[]
                {
                    new ChatGroup("Private chats", chats.Where(x => x.Kind == ChatKind.Private)),
                    new ChatGroup("Groups", chats.Where(x => x.Kind == ChatKind.Group)),
                    new ChatGroup("Channels", chats.Where(x => x.Kind == ChatKind.Channel))
                }
                .Where(x => x.Count > 0)
                .ToList();
            ShowChatsStatus(chats.Count == 0 ? "No chats" : null);
        }
        catch (Exception ex)
        {
            ShowChatsStatus(ForwardingError.Describe(ex));
        }
    }

    private async void Provider_OnToggled(object? sender, ToggledEventArgs e)
    {
        if (sender is not Switch { BindingContext: ProviderRow row } toggle || e.Value == row.Enabled)
            return;

        try
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.PutAsJsonAsync($"{Preferences.Get("backend", "")}/providers/{row.Target}", new ProviderSwitch(e.Value));
            response.EnsureSuccessStatusCode();
            row.Enabled = e.Value;
            ShowStatus(null);
        }
        catch (Exception ex)
        {
            toggle.IsToggled = row.Enabled;
            ShowStatus(ForwardingError.Describe(ex));
        }
    }

    private async void Chat_OnToggled(object? sender, ToggledEventArgs e)
    {
        // Also raised when a row is shown or reused for another chat while scrolling.
        if (sender is not Switch { BindingContext: ChatRow row } || e.Value == row.Enabled)
            return;

        try
        {
            using var httpClient = new HttpClient();
            var response = await httpClient.PutAsJsonAsync(
                $"{Preferences.Get("backend", "")}/providers/{Target.Telegram}/chats/{Uri.EscapeDataString(row.ChatId)}", new ProviderSwitch(e.Value));
            response.EnsureSuccessStatusCode();
            row.Enabled = e.Value;
            ShowChatsStatus(null);
        }
        catch (Exception ex)
        {
            row.ResetSwitch();
            ShowChatsStatus(ForwardingError.Describe(ex));
        }
    }

    private void ShowStatus(string? text)
    {
        Status.Text = text;
        Status.IsVisible = text != null;
    }

    private void ShowChatsStatus(string? text)
    {
        ChatsStatus.Text = text;
        ChatsStatus.IsVisible = text != null;
    }
}

public class ProviderRow(ProviderStatus status)
{
    public Target Target { get; } = status.Target;
    public string Title { get; } = status.Configured ? status.Target.ToString() : $"{status.Target} (not set up)";
    public bool Configured { get; } = status.Configured;
    public bool Enabled { get; set; } = status.Enabled;
}

public class ChatGroup(string title, IEnumerable<ChatStatus> chats) : List<ChatRow>(chats.Select(x => new ChatRow(x)))
{
    public string Title { get; } = title;
}

public class ChatRow(ChatStatus status) : INotifyPropertyChanged
{
    public string ChatId { get; } = status.ChatId;
    public string Title { get; } = status.Title;
    public bool Enabled { get; set; } = status.Enabled;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void ResetSwitch() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
}
