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
#if ANDROID
        // The service does not survive a reboot or a force stop, so start it again if it was left on.
        if (AutoForward.IsToggled)
            ForwardingService.Start();
#endif
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ForwardingHistory.Changed += OnHistoryChanged;
        ShowHistory();
    }

    protected override void OnDisappearing()
    {
        ForwardingHistory.Changed -= OnHistoryChanged;
        base.OnDisappearing();
    }

    // Raised by the background service too, so it may arrive on a non-UI thread.
    private void OnHistoryChanged() => MainThread.BeginInvokeOnMainThread(ShowHistory);

    private void ShowHistory()
    {
        History.ItemsSource = ForwardingHistory.GetEntries()
            .Select(x => $"{(x.Time.Date == DateTime.Today ? x.Time.ToString("HH:mm") : x.Time.ToString("dd MMM HH:mm"))}  {x.Source}  {x.Text}")
            .ToList();
    }

    private async Task<List<string>> GetMessages()
    {
        var res = await CheckAndRequestSmsPermission();
        List<string> items = [];
        if (res.Equals(PermissionStatus.Granted))
        {
#if ANDROID
            string inbox = "content://sms/inbox";
            string[] reqCols = ["_id", "thread_id", "address", "person", "date", "body", "type"];
            var uri = Android.Net.Uri.Parse(inbox);
            var cursor = Platform.CurrentActivity?.ContentResolver?.Query(uri, reqCols, null, null, null);

            if (cursor?.MoveToFirst() == true)
            {
                do
                {
                    var messageId = cursor.GetString(cursor.GetColumnIndex(reqCols[0]));
                    var threadId = cursor.GetString(cursor.GetColumnIndex(reqCols[1]));
                    var address = cursor.GetString(cursor.GetColumnIndex(reqCols[2]));
                    var name = cursor.GetString(cursor.GetColumnIndex(reqCols[3]));
                    var date = cursor.GetString(cursor.GetColumnIndex(reqCols[4]));
                    var msg = cursor.GetString(cursor.GetColumnIndex(reqCols[5]));
                    var type = cursor.GetString(cursor.GetColumnIndex(reqCols[6]));

                    items.Add(messageId + (","
                                           + (threadId + (","
                                                          + (address + (","
                                                                        + (name + (","
                                                                            + (date + (" ,"
                                                                                + (msg + (" ," + type))))))))))));
                } while (cursor.MoveToNext());
            }
#endif
        }

        return items;
    }

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

            Messages.ItemsSource = await GetMessages();
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
#if ANDROID
            if (e.Value)
            {
                await Permissions.RequestAsync<Permissions.PostNotifications>();
                ForwardingService.Start();
            }
            else
            {
                ForwardingService.Stop();
            }
#endif
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

#if ANDROID
        if (AndroidX.Core.Content.ContextCompat.CheckSelfPermission(Platform.CurrentActivity, Android.Manifest.Permission.SendSms) != Android.Content.PM.Permission.Granted)
        {
            AndroidX.Core.App.ActivityCompat.RequestPermissions(Platform.CurrentActivity, new string[] { Android.Manifest.Permission.SendSms }, 1);
            await Notify("Allow sending SMS and try again");
            return false;
        }
#endif

        return true;
    }

    private async Task Notify(string text)
    {
#if ANDROID
        await Toast.Make(text).Show();
#else
        await DisplayAlertAsync("CommunicationApp", text, "OK");
#endif
    }

    private void Backend_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        Preferences.Set("backend", Backend.Text);
    }
}