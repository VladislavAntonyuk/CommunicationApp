using Android.App;
using Android.Content;
using Android.Util;

namespace CommunicationApp;

[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(["android.provider.Telephony.SMS_RECEIVED"])]
public class SmsReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        try
        {
            if (context == null || intent?.Action != "android.provider.Telephony.SMS_RECEIVED")
                return;

            var messages = Android.Provider.Telephony.Sms.Intents.GetMessagesFromIntent(intent);
            if (messages == null || messages.Length == 0)
                return;

            if (Shared.PhoneNumberMatcher.IsSameNumber(messages[0].OriginatingAddress, Preferences.Get("from", null)))
                IncomingSmsWorker.Enqueue(context, string.Concat(messages.Select(m => m.MessageBody)));
        }
        catch (Exception ex)
        {
            Log.Error(nameof(SmsReceiver), $"Could not queue the incoming SMS: {ex}");
        }
    }
}
