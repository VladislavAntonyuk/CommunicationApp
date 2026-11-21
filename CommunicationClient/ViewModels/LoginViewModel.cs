using CommunicationClient.Pages;
using CommunicationClient.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Shared.Api;

namespace CommunicationClient.ViewModels;

public partial class LoginViewModel(IAuthApi auth, AuthSession session, INavigationService navigation) : ObservableObject
{
    [ObservableProperty]
    public partial string PhoneNumber { get; set; } = "";

    [ObservableProperty]
    public partial string Code { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AsksForPhone), nameof(AsksForCode), nameof(Prompt))]
    public partial string? SentTo { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReady), nameof(AsksForPhone), nameof(AsksForCode))]
    [NotifyCanExecuteChangedFor(nameof(SendCodeCommand), nameof(LogInCommand), nameof(ChangeNumberCommand))]
    public partial bool IsBusy { get; set; } = true;

    public bool IsReady => !IsBusy;

    public bool AsksForPhone => !IsBusy && SentTo == null;

    public bool AsksForCode => !IsBusy && SentTo != null;

    public string Prompt => SentTo == null
        ? "Your phone number. We will send a login code to it by SMS."
        : $"Code sent by SMS to {SentTo}";

    [RelayCommand]
    private async Task Load()
    {
        if (await session.IsLoggedIn())
        {
            await navigation.GoTo($"//{nameof(ProvidersPage)}");
            return;
        }

        IsBusy = false;
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendCode()
    {
        var phoneNumber = SentTo ?? PhoneNumber;
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return;

        await Run(async () =>
        {
            var response = await auth.RequestCode(new LoginPhone(phoneNumber));
            if (!response.IsSuccessfulWithContent)
            {
                Error = response.ErrorMessage;
                return;
            }

            SentTo = response.Content.PhoneNumber;
            Code = "";
        });
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task LogIn()
    {
        if (SentTo == null || string.IsNullOrWhiteSpace(Code))
            return;

        var request = new LoginRequest(SentTo, Code);
        await Run(async () =>
        {
            var response = await auth.Login(request);
            if (!response.IsSuccessfulWithContent)
            {
                Error = response.ErrorMessage;
                return;
            }

            await session.LogIn(response.Content);
            SentTo = null;
            Code = "";
            await navigation.GoTo($"//{nameof(ProvidersPage)}");
        });
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private void ChangeNumber()
    {
        SentTo = null;
        Code = "";
        Error = null;
    }

    private bool CanSend() => !IsBusy;

    private async Task Run(Func<Task> action)
    {
        if (IsBusy)
            return;

        IsBusy = true;
        Error = null;
        try
        {
            await action();
        }
        finally
        {
            IsBusy = false;
        }
    }
}
