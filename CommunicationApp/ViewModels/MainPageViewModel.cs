using CommunicationApp.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommunicationApp.ViewModels;

public partial class MainPageViewModel(
    ForwardingStatus status,
    IPreferences preferences,
    ISmsPermissions permissions,
    IForwardingControl forwarding,
    INotifier notifier) : ObservableObject
{
    [ObservableProperty]
    public partial bool AutoForward { get; set; } = preferences.Get("autoForward", false);

    [ObservableProperty]
    public partial string Status { get; set; } = status.Text;

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
