using System.Net.Http.Json;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Widget;
using Object = Java.Lang.Object;

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

            Bundle? bundle = intent.Extras;
            if (bundle == null) return;

            var pdus = (Object[]?)bundle.Get("pdus");
            if (pdus == null) return;

            foreach (var pdu in pdus)
            {
                Android.Telephony.SmsMessage? message = Android.Telephony.SmsMessage.CreateFromPdu((byte[])pdu, bundle.GetString("format"));
                string? sender = message?.OriginatingAddress;
                string? body = message?.MessageBody;

                var recipient = Preferences.Get("recipient", null);
                if (sender == recipient)
                {
                    using var httpClient = new HttpClient();
                    await httpClient.PostAsJsonAsync("https://localhost:7001/send", new Shared.Message(body, Shared.Target.Sms));
                }
            }
        }
        catch (Exception ex)
        {
            Toast.MakeText(context, "Error receiving SMS: " + ex.Message, ToastLength.Long).Show();
        }
    }
}