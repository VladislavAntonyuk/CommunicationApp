using Android.Content.PM;
using AndroidX.Core.App;
using AndroidX.Core.Content;

namespace CommunicationApp.Services;

public interface ISmsPermissions
{
    Task<bool> RequestReceive();
    bool RequestSend();
    Task RequestNotifications();
}

public class AndroidSmsPermissions : ISmsPermissions
{
    public async Task<bool> RequestReceive()
    {
        if (await Permissions.CheckStatusAsync<Permissions.Sms>() == PermissionStatus.Granted)
            return true;

        return await Permissions.RequestAsync<Permissions.Sms>() == PermissionStatus.Granted;
    }

    public bool RequestSend()
    {
        var activity = Platform.CurrentActivity!;
        if (ContextCompat.CheckSelfPermission(activity, Android.Manifest.Permission.SendSms) == Permission.Granted)
            return true;

        ActivityCompat.RequestPermissions(activity, [Android.Manifest.Permission.SendSms], 1);
        return false;
    }

    public Task RequestNotifications() => Permissions.RequestAsync<Permissions.PostNotifications>();
}
