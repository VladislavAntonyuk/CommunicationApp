using CommunityToolkit.Maui.Alerts;

namespace CommunicationApp;

public partial class MainPage : ContentPage
{
    private readonly bool _loading;

    public MainPage()
    {
        InitializeComponent();
        PhoneNumber.Text = Preferences.Get("from", "");
        Backend.Text = Preferences.Get("backend", "");

        _loading = true;
        AutoForward.IsToggled = Preferences.Get("autoForward", false);
        _loading = false;
        // The service does not survive a reboot or a force stop, so start it again if it was left on.
        if (AutoForward.IsToggled)
            ForwardingService.Start();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ForwardingStatus.Changed += OnStatusChanged;
        ShowStatus();
    }

    protected override void OnDisappearing()
    {
        ForwardingStatus.Changed -= OnStatusChanged;
        base.OnDisappearing();
    }

    // Raised by the background service too, so it may arrive on a non-UI thread.
    private void OnStatusChanged() => MainThread.BeginInvokeOnMainThread(ShowStatus);

    private void ShowStatus() => Status.Text = ForwardingStatus.Text;

    private async Task<PermissionStatus> CheckAndRequestSmsPermission()
    {
        PermissionStatus status = await Permissions.CheckStatusAsync<Permissions.Sms>();

        if (status == PermissionStatus.Granted)
            return status;

        status = await Permissions.RequestAsync<Permissions.Sms>();
        return status;
    }

    private void PhoneNumber_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        Preferences.Set("from", PhoneNumber.Text);
    }

    private async void CallButton_OnClicked(object? sender, EventArgs e)
    {
        try
        {
            if (!await EnsureReadyToForward())
                return;

            var sent = await MessageForwarder.ForwardPending();

            await Notify($"{sent} messages sent");
        }
        catch (Exception ex)
        {
            await Notify(ex.Message);
        }
    }

    private async void AutoForward_OnToggled(object? sender, ToggledEventArgs e)
    {
        if (_loading)
            return;

        try
        {
            if (e.Value && !await EnsureReadyToForward())
            {
                AutoForward.IsToggled = false;
                return;
            }

            Preferences.Set("autoForward", e.Value);
            if (e.Value)
            {
                await Permissions.RequestAsync<Permissions.PostNotifications>();
                ForwardingService.Start();
            }
            else
            {
                ForwardingService.Stop();
            }
        }
        catch (Exception ex)
        {
            await Notify(ex.Message);
        }
    }

    private async Task<bool> EnsureReadyToForward()
    {
        if (!Uri.TryCreate(Backend.Text, UriKind.Absolute, out var backend) || (backend.Scheme != Uri.UriSchemeHttp && backend.Scheme != Uri.UriSchemeHttps))
        {
            await Notify("Enter a valid backend URL");
            return false;
        }

        if (string.IsNullOrWhiteSpace(PhoneNumber.Text))
        {
            await Notify("Enter a phone number");
            return false;
        }

        if (await CheckAndRequestSmsPermission() != PermissionStatus.Granted)
        {
            await Notify("SMS permission is required");
            return false;
        }

        if (AndroidX.Core.Content.ContextCompat.CheckSelfPermission(Platform.CurrentActivity, Android.Manifest.Permission.SendSms) != Android.Content.PM.Permission.Granted)
        {
            AndroidX.Core.App.ActivityCompat.RequestPermissions(Platform.CurrentActivity, new string[] { Android.Manifest.Permission.SendSms }, 1);
            await Notify("Allow sending SMS and try again");
            return false;
        }

        return true;
    }

    private async Task Notify(string text)
    {
        await Toast.Make(text).Show();
    }

    private void Backend_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        Preferences.Set("backend", Backend.Text);
    }
}