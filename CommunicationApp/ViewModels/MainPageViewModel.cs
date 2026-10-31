using CommunicationApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommunicationApp.ViewModels;

public partial class MainPageViewModel(
    MessageForwarder forwarder,
    ForwardingStatus status,
    IPreferences preferences,
    ISmsPermissions permissions,
    IForwardingControl forwarding,
    INotifier notifier) : ObservableObject
{
    [ObservableProperty]
    public partial string PhoneNumber { get; set; } = preferences.Get("from", "");

    [ObservableProperty]
    public partial bool AutoForward { get; set; } = preferences.Get("autoForward", false);

    [ObservableProperty]
    public partial string Status { get; set; } = status.Text;

    partial void OnPhoneNumberChanged(string value) => preferences.Set("from", value);

    [RelayCommand]
    private void Appearing()
    {
        status.Changed += OnStatusChanged;
        Status = status.Text;
        // The service does not survive a reboot or a force stop, so start it again if it was left on.
        if (AutoForward)
            forwarding.Start();
    }

    [RelayCommand]
    private void Disappearing() => status.Changed -= OnStatusChanged;

    [RelayCommand]
    private async Task ForwardNow()
    {
        if (!await EnsureReadyToForward())
            return;

        var result = await forwarder.ForwardPending();
        await notifier.Show(result.Error == null
            ? $"{result.Sent} messages sent"
            : $"Error after {result.Sent} messages sent: {result.Error}");
    }

    [RelayCommand]
    private async Task ToggleAutoForward()
    {
        var enable = !AutoForward;
        if (enable && !await EnsureReadyToForward())
            return;

        AutoForward = enable;
        preferences.Set("autoForward", enable);
        if (enable)
        {
            await permissions.RequestNotifications();
            forwarding.Start();
        }
        else
        {
            forwarding.Stop();
        }
    }

    // Raised by the background service too, so it may arrive on a non-UI thread.
    private void OnStatusChanged() => MainThread.BeginInvokeOnMainThread(() => Status = status.Text);

    private async Task<bool> EnsureReadyToForward()
    {
        if (string.IsNullOrWhiteSpace(PhoneNumber))
        {
            await notifier.Show("Enter a phone number");
            return false;
        }

        if (!await permissions.RequestReceive())
        {
            await notifier.Show("SMS permission is required");
            return false;
        }

        if (!permissions.RequestSend())
        {
            await notifier.Show("Allow sending SMS and try again");
            return false;
        }

        return true;
    }
}
