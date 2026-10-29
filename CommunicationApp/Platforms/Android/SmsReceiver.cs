using System.Net.Http.Json;
using Android.App;
using Android.Content;
using Android.Widget;
using Microsoft.Extensions.Configuration;

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
                using var httpClient = new HttpClient();
                var backend = IPlatformApplication.Current!.Services.GetRequiredService<IConfiguration>()["Backend"];
                var result = await httpClient.PostAsJsonAsync($"{backend}/send", new Shared.Message(body));
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
        catch (Exception ex)
        {
            Toast.MakeText(context, "Error receiving SMS: " + ex.Message, ToastLength.Long)?.Show();
        }
    }
}