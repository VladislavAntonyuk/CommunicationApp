using System.Net.Http.Json;
using Android.App;
using Android.Content;
using Android.Widget;

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
            if (IsSameNumber(sender, from))
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
        catch (Exception ex)
        {
            Toast.MakeText(context, "Error receiving SMS: " + ex.Message, ToastLength.Long)?.Show();
        }
    }

    private static bool IsSameNumber(string? sender, string? from)
    {
        if (!string.IsNullOrEmpty(sender) && sender == from)
            return true;

        var a = Digits(sender);
        var b = Digits(from);
        if (a.Length == 0 || b.Length == 0)
            return false;

        // The same number may arrive with or without a country code.
        var (longer, shorter) = a.Length >= b.Length ? (a, b) : (b, a);
        return longer == shorter || (shorter.Length >= 9 && longer.EndsWith(shorter));
    }

    private static string Digits(string? number) =>
        new string((number ?? "").Where(char.IsDigit).ToArray()).TrimStart('0');
}