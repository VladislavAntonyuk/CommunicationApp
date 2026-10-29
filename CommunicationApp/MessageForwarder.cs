using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Shared;

namespace CommunicationApp;

public static class MessageForwarder
{
    // The button and the background service must not forward at the same time, or the same message is sent twice.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<int> ForwardPending()
    {
        await Gate.WaitAsync();
        var sent = 0;
        try
        {
            var backend = IPlatformApplication.Current!.Services.GetRequiredService<IConfiguration>()["Backend"];
            var phoneNumber = Preferences.Get("from", "");

            using var httpClient = new HttpClient();
            var messages = await httpClient.GetFromJsonAsync<List<ReceivedMessage>>($"{backend}/receive") ?? [];
            foreach (var item in messages)
            {
                SendSms(phoneNumber, item.Text);
                var response = await httpClient.PostAsJsonAsync($"{backend}/receive/ack", new Acknowledgement([item.Id]));
                response.EnsureSuccessStatusCode();
                sent++;
            }

            ForwardingStatus.Succeeded(sent);
            return sent;
        }
        catch (Exception ex)
        {
            var reason = ForwardingError.Describe(ex);
            ForwardingStatus.Failed(sent, reason);
            throw new InvalidOperationException($"Error after {sent} messages sent: {reason}", ex);
        }
        finally
        {
            Gate.Release();
        }
    }

    private static void SendSms(string phoneNumber, string message)
    {
        try
        {
            var smsManager = (Android.Telephony.SmsManager?)Android.App.Application.Context.GetSystemService(Java.Lang.Class.FromType(typeof(Android.Telephony.SmsManager)));

            if (smsManager == null)
                throw new InvalidOperationException("SMS manager is not available");

            var parts = smsManager.DivideMessage(message);
            if (parts == null || parts.Count <= 1)
                smsManager.SendTextMessage(phoneNumber, null, message, null, null);
            else
                smsManager.SendMultipartTextMessage(phoneNumber, null, parts, null, null);
        }
        catch (Exception ex)
        {
            throw new SmsFailedException($"Could not send SMS: {ex.Message}", ex);
        }
    }

    private sealed class SmsFailedException(string message, Exception inner) : Exception(message, inner);
}
