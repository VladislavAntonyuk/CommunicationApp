using System.Net.Http.Json;
using Shared;

namespace CommunicationClient;

public partial class TelegramLoginPage : ContentPage
{
    private bool _busy;

    public TelegramLoginPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Send(httpClient => httpClient.GetAsync($"{Backend}/telegram/login"));
    }

    private static string Backend => Preferences.Get("backend", "");

    private async void Continue_OnClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Value.Text))
            return;

        var value = Value.Text;
        await Send(httpClient => httpClient.PostAsJsonAsync($"{Backend}/telegram/login", new TelegramLoginValue(value)));
    }

    private async void StartOver_OnClicked(object? sender, EventArgs e)
    {
        await Send(httpClient => httpClient.PostAsync($"{Backend}/telegram/logout", null));
    }

    private async Task Send(Func<HttpClient, Task<HttpResponseMessage>> request)
    {
        if (_busy)
            return;

        _busy = true;
        Continue.IsEnabled = false;
        StartOver.IsEnabled = false;
        ShowError(null);
        try
        {
            using var httpClient = new HttpClient();
            var response = await request(httpClient);
            if (!response.IsSuccessStatusCode)
            {
                ShowError(await ServerError.Read(response));
                response = await httpClient.GetAsync($"{Backend}/telegram/login");
                if (!response.IsSuccessStatusCode)
                    return;
            }

            var status = await response.Content.ReadFromJsonAsync<TelegramLoginStatus>();
            if (status != null)
                await Show(status);
        }
        catch (Exception ex)
        {
            ShowError(ForwardingError.Describe(ex));
        }
        finally
        {
            _busy = false;
            Continue.IsEnabled = true;
            StartOver.IsEnabled = true;
        }
    }

    private async Task Show(TelegramLoginStatus status)
    {
        if (status.Step == TelegramLoginStep.LoggedIn)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        var asksForValue = status.Step != TelegramLoginStep.NotConfigured;
        Prompt.Text = status.Step switch
        {
            TelegramLoginStep.Phone => "Phone number of your Telegram account, with the country code",
            TelegramLoginStep.Code => "Code that Telegram sent to your Telegram app or by SMS",
            TelegramLoginStep.Password => "Your two-step verification password",
            _ => "Telegram is not set up on the server"
        };
        Value.Text = "";
        Value.Placeholder = status.Step switch
        {
            TelegramLoginStep.Phone => "+380987654321",
            TelegramLoginStep.Code => "12345",
            _ => ""
        };
        Value.Keyboard = status.Step switch
        {
            TelegramLoginStep.Phone => Keyboard.Telephone,
            TelegramLoginStep.Code => Keyboard.Numeric,
            _ => Keyboard.Default
        };
        Value.IsPassword = status.Step == TelegramLoginStep.Password;
        Value.IsVisible = asksForValue;
        Continue.IsVisible = asksForValue;
        StartOver.IsVisible = status.Step is TelegramLoginStep.Code or TelegramLoginStep.Password;
    }

    private void ShowError(string? text)
    {
        Error.Text = text;
        Error.IsVisible = text != null;
    }
}
