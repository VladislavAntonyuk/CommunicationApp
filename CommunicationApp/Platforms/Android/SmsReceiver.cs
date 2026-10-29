using Android.App;
using Android.Content;
using Android.Widget;
using CommunicationApp.Services;

namespace CommunicationApp;

[BroadcastReceiver(Enabled = true, Exported = true)]
[IntentFilter(["android.provider.Telephony.SMS_RECEIVED"])]
public class SmsReceiver : BroadcastReceiver
{
    public override async void OnReceive(Context? context, Intent? intent)
    {
        try
        {
            if (intent?.Action != "android.provider.Telephony.SMS_RECEIVED")
                return;

            Android.Telephony.SmsMessage[]? messages = Android.Provider.Telephony.Sms.Intents.GetMessagesFromIntent(intent);
            if (messages == null || messages.Length == 0) return;

            string? sender = messages[0].OriginatingAddress;
            string body = string.Concat(messages.Select(m => m.MessageBody));

            var from = Preferences.Get("from", null);
            if (Shared.PhoneNumberMatcher.IsSameNumber(sender, from))
            {
                var api = IPlatformApplication.Current!.Services.GetRequiredService<IGatewayApi>();
                var result = await api.Send(new Shared.Message(body));
                if (result.IsSuccessful)
                {
                    Toast.MakeText(context, "SMS sent to API successfully!", ToastLength.Short)?.Show();
                }
                else
                {
                    Toast.MakeText(context, "Failed to send SMS to API: " + result.ErrorMessage, ToastLength.Long)?.Show();
                }
            }
        }
        catch (Exception ex)
        {
            Toast.MakeText(context, "Error receiving SMS: " + ex.Message, ToastLength.Long)?.Show();
        }
    }
}