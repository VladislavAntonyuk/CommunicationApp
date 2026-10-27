using CommunicationClient.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared;

namespace CommunicationClient.ViewModels;

public partial class TelegramLoginViewModel(ApiClient api, INavigationService navigation) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Prompt), nameof(Placeholder), nameof(IsPassword), nameof(AsksForValue), nameof(CanStartOver))]
    public partial TelegramLoginStep? Step { get; set; }

    [ObservableProperty]
    public partial string Value { get; set; } = "";

    [ObservableProperty]
    public partial string? Error { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand), nameof(StartOverCommand))]
    public partial bool IsBusy { get; set; }

    public string Prompt => Step switch
    {
        null => "Loading...",
        TelegramLoginStep.Phone => "Phone number of your Telegram account, with the country code",
        TelegramLoginStep.Code => "Code that Telegram sent to your Telegram app or by SMS",
        TelegramLoginStep.Password => "Your two-step verification password",
        _ => "Telegram is not set up on the server"
    };

    public string Placeholder => Step switch
    {
        TelegramLoginStep.Phone => "+380987654321",
        TelegramLoginStep.Code => "12345",
        _ => ""
    };

    public bool IsPassword => Step == TelegramLoginStep.Password;

    public bool AsksForValue => Step is not null and not TelegramLoginStep.NotConfigured;

    public bool CanStartOver => Step is TelegramLoginStep.Code or TelegramLoginStep.Password;

    [RelayCommand]
    private Task Load() => Send(api.GetTelegramLogin);

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task Continue()
    {
        if (string.IsNullOrWhiteSpace(Value))
            return;

        var value = Value;
        await Send(() => api.SendTelegramLogin(value));
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private Task StartOver() => Send(api.TelegramLogout);

    private bool CanSend() => !IsBusy;

    private async Task Send(Func<Task<TelegramLoginStatus?>> request)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        Error = null;
        try
        {
            TelegramLoginStatus? status;
            try
            {
                status = await request();
            }
            catch (ServerException ex)
            {
                Error = ex.Message;
                status = await CurrentStatus();
            }

            if (status != null)
                await Show(status);
        }
        catch (Exception ex)
        {
            Error = ForwardingError.Describe(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<TelegramLoginStatus?> CurrentStatus()
    {
        try
        {
            return await api.GetTelegramLogin();
        }
        catch (ServerException)
        {
            return null;
        }
    }

    private async Task Show(TelegramLoginStatus status)
    {
        if (status.Step == TelegramLoginStep.LoggedIn)
        {
            await navigation.GoBack();
            return;
        }

        Step = status.Step;
        Value = "";
    }
}
