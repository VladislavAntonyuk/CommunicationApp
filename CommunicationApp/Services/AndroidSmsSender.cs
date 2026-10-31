using Android.Telephony;

namespace CommunicationApp.Services;

public sealed class AndroidSmsSender : ISmsSender
{
    public void Send(string phoneNumber, string text)
    {
        var smsManager = (SmsManager?)Android.App.Application.Context.GetSystemService(Java.Lang.Class.FromType(typeof(SmsManager)))
            ?? throw new InvalidOperationException("SMS manager is not available");

        var parts = smsManager.DivideMessage(text);
        if (parts == null || parts.Count <= 1)
            smsManager.SendTextMessage(phoneNumber, null, text, null, null);
        else
            smsManager.SendMultipartTextMessage(phoneNumber, null, parts, null, null);
    }
}
