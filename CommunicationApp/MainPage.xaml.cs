namespace CommunicationApp;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        PhoneNumber.Text = Preferences.Get("recipient", "");
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
            var cursor = Platform.CurrentActivity?.ContentResolver?.Query(uri, reqCols, null, null,
                    null);

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
        Preferences.Set("recipient", PhoneNumber.Text);
    }

    private async void CallButton_OnClicked(object? sender, EventArgs e)
    {
        Messages.ItemsSource = await GetMessages();
    }
}