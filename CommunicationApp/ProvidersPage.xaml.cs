using System.ComponentModel;
using System.Net.Http.Json;
using Shared;

namespace CommunicationApp;

public partial class ProvidersPage : ContentPage
{
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
                await LoadChats(httpClient, backend);
        }
        catch (Exception ex)
        {
            ShowStatus(ForwardingError.Describe(ex));
        }
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
                // The server explains the failure, for example "Telegram is not logged in".
                var problem = await response.Content.ReadFromJsonAsync<Problem>();
                ShowChatsStatus(problem?.Detail ?? $"Server error ({(int)response.StatusCode})");
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
        // Also raised when the row is first shown with its saved value.
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

    private sealed record Problem(string? Detail);
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

    // Puts the switch back to the saved value after a failed save.
    public void ResetSwitch() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Enabled)));
}
