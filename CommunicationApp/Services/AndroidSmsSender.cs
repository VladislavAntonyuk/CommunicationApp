using Android.App;
using Android.Content;
using Android.Telephony;
using AndroidX.Core.Content;

namespace CommunicationApp.Services;

public sealed class AndroidSmsSender : ISmsSender
{
    private static readonly TimeSpan ResultTimeout = TimeSpan.FromSeconds(30);

    public async Task Send(string phoneNumber, string text)
    {
        var context = Android.App.Application.Context;
        var smsManager = (OperatingSystem.IsAndroidVersionAtLeast(31)
                ? (SmsManager?)context.GetSystemService(Java.Lang.Class.FromType(typeof(SmsManager)))
                : SmsManager.Default)
            ?? throw new InvalidOperationException("SMS manager is not available");

        var parts = smsManager.DivideMessage(text) ?? [text];
        var action = $"{context.PackageName}.SMS_SENT.{Guid.NewGuid():N}";
        var receiver = new SentReceiver(parts.Count);
        ContextCompat.RegisterReceiver(context, receiver, new IntentFilter(action), ContextCompat.ReceiverNotExported);
        try
        {
            var sentIntents = parts
                .Select((_, i) => PendingIntent.GetBroadcast(context, i, new Intent(action).SetPackage(context.PackageName), PendingIntentFlags.Immutable)!)
                .ToList();

            smsManager.SendMultipartTextMessage(phoneNumber, null, parts, sentIntents, null);
            await receiver.Sent.WaitAsync(ResultTimeout);
        }
        finally
        {
            context.UnregisterReceiver(receiver);
        }
    }

    private sealed class SentReceiver(int parts) : BroadcastReceiver
    {
        private readonly TaskCompletionSource _result = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _remaining = parts;

        public Task Sent => _result.Task;

        public override void OnReceive(Context? context, Intent? intent)
        {
            if (ResultCode != Result.Ok)
                _result.TrySetException(new InvalidOperationException(((SmsResultError)(int)ResultCode).ToString()));
            else if (--_remaining == 0)
                _result.TrySetResult();
        }
    }
}
