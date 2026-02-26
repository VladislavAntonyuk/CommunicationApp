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

                var from = Preferences.Get("from", null);
                if (sender == from)
                {
                    using var httpClient = new HttpClient();
                    var result = await httpClient.PostAsJsonAsync($"{Preferences.Get("backend", "")}/send", new Shared.Message(body));
                    if (result.IsSuccessStatusCode)
                    {
                        Toast.MakeText(context, "SMS sent to API successfully!", ToastLength.Short)?.Show();
                    }
                    else
                    {
                        Toast.MakeText(context, "Failed to send SMS to API: " + result.ReasonPhrase, ToastLength.Long)?.Show();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Toast.MakeText(context, "Error receiving SMS: " + ex.Message, ToastLength.Long)?.Show();
        }
    }
}