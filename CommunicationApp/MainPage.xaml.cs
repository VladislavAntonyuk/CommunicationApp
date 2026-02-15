using System.Net.Http.Json;
using CommunityToolkit.Maui.Alerts;
using Shared;

namespace CommunicationApp;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        PhoneNumber.Text = Preferences.Get("from", "");
        Backend.Text = Preferences.Get("backend", "");
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
        Messages.ItemsSource = await GetMessages();
        var messages = await ReceiveMessages();
#if ANDROID
        if (AndroidX.Core.Content.ContextCompat.CheckSelfPermission(Platform.CurrentActivity, Android.Manifest.Permission.SendSms) != Android.Content.PM.Permission.Granted)
        {
            AndroidX.Core.App.ActivityCompat.RequestPermissions(Platform.CurrentActivity, new string[] { Android.Manifest.Permission.SendSms }, 1);
        }
#endif
        foreach (var item in messages)
        {
            SendSms(item.Text);
        }

        await Toast.Make($"{messages.Count} messages sent").Show();
    }

    private async Task<List<Message>> ReceiveMessages()
    {
        using var httpClient = new HttpClient();
        return await httpClient.GetFromJsonAsync<List<Message>>($"{Preferences.Get("backend", "")}/receive") ?? [];
    }

    private void SendSms(string message)
    {
#if ANDROID
        Android.Telephony.SmsManager.Default.SendTextMessage(PhoneNumber.Text, null, message, null, null);
#endif
    }

    private void Backend_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        Preferences.Set("backend", Backend.Text);
    }
}