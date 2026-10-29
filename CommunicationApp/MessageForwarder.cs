using CommunicationApp.Services;
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
            var api = IPlatformApplication.Current!.Services.GetRequiredService<IGatewayApi>();
            var phoneNumber = Preferences.Get("from", "");

            var messages = await api.GetPending();
            if (!messages.IsSuccessfulWithContent)
                throw new InvalidOperationException(messages.ErrorMessage);

            foreach (var item in messages.Content)
            {
                SendSms(phoneNumber, item.Text);
                var response = await api.Acknowledge(new Acknowledgement([item.Id]));
                if (!response.IsSuccessful)
                    throw new InvalidOperationException(response.ErrorMessage);
                sent++;
            }

            ForwardingStatus.Succeeded(sent);
            return sent;
        }
        catch (Exception ex)
        {
            ForwardingStatus.Failed(sent, ex.Message);
            throw new InvalidOperationException($"Error after {sent} messages sent: {ex.Message}", ex);
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
