namespace CommunicationApp;

public partial class MainPage : ContentPage
{
    int count = 0;

    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnCounterClicked(object sender, EventArgs e)
    {
        var res= await  CheckAndRequestSMSPermission();
        if (res.Equals( PermissionStatus.Granted))
        {
#if ANDROID

            List<string> items=new List<string>();
            string INBOX = "content://sms/inbox";
            string[] reqCols = new string[] { "_id", "thread_id", "address", "person", "date", "body", "type" };
            Android.Net.Uri uri = Android.Net.Uri.Parse(INBOX);
            Android.Database.ICursor cursor = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity.ContentResolver.Query(uri, reqCols, null, null, null);

            if (cursor.MoveToFirst())
            {
                do
                {
                    string messageId = cursor.GetString(cursor.GetColumnIndex(reqCols[0]));
                    string threadId = cursor.GetString(cursor.GetColumnIndex(reqCols[1]));
                    string address = cursor.GetString(cursor.GetColumnIndex(reqCols[2]));
                    string name = cursor.GetString(cursor.GetColumnIndex(reqCols[3]));
                    string date = cursor.GetString(cursor.GetColumnIndex(reqCols[4]));
                    string msg = cursor.GetString(cursor.GetColumnIndex(reqCols[5]));
                    string type = cursor.GetString(cursor.GetColumnIndex(reqCols[6]));

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
    }
    
    public async Task<PermissionStatus> CheckAndRequestSMSPermission()
    {
        PermissionStatus status = await Permissions.CheckStatusAsync<Permissions.Sms>();

        if (status == PermissionStatus.Granted)
            return status;

        if (status == PermissionStatus.Denied && DeviceInfo.Platform == DevicePlatform.iOS)
        {
            // Prompt the user to turn on in settings
            // On iOS once a permission has been denied it may not be requested again from the application
            return status;
        }

        if (Permissions.ShouldShowRationale<Permissions.Sms>())
        {
            // Prompt the user with additional information as to why the permission is needed
        }
        status = await Permissions.RequestAsync<Permissions.Sms>();
        return status;
    }
}