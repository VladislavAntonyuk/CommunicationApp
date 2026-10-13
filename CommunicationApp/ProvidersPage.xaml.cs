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
        }
        catch (Exception ex)
        {
            ShowStatus(ForwardingError.Describe(ex));
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

    private void ShowStatus(string? text)
    {
        Status.Text = text;
        Status.IsVisible = text != null;
    }
}

public class ProviderRow(ProviderStatus status)
{
    public Target Target { get; } = status.Target;
    public string Title { get; } = status.Configured ? status.Target.ToString() : $"{status.Target} (not set up)";
    public bool Configured { get; } = status.Configured;
    public bool Enabled { get; set; } = status.Enabled;
}
