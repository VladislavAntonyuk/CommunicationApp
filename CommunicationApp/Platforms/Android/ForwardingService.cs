using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

namespace CommunicationApp;

[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeSpecialUse)]
public class ForwardingService : Service
{
    private const string ChannelId = "forwarding";
    private const int NotificationId = 1;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private const string IdleText = "Forwarding messages";

    private CancellationTokenSource? _cts;
    private string _lastForwardedText = IdleText;
    private string _shownText = IdleText;

    public static void Start()
    {
        var context = Android.App.Application.Context;
        AndroidX.Core.Content.ContextCompat.StartForegroundService(context, new Intent(context, typeof(ForwardingService)));
    }

    public static void Stop()
    {
        var context = Android.App.Application.Context;
        context.StopService(new Intent(context, typeof(ForwardingService)));
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var type = OperatingSystem.IsAndroidVersionAtLeast(34) ? (int)ForegroundService.TypeSpecialUse : 0;
        ServiceCompat.StartForeground(this, NotificationId, BuildNotification(_shownText), type);

        if (_cts == null)
        {
            _cts = new CancellationTokenSource();
            _ = Run(_cts.Token);
        }

        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        _cts?.Cancel();
        _cts = null;
        base.OnDestroy();
    }

    private async Task Run(CancellationToken token)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            do
            {
                try
                {
                    var sent = await MessageForwarder.ForwardPending();
                    if (sent > 0)
                        _lastForwardedText = $"Last forwarded: {sent} {(sent == 1 ? "message" : "messages")} at {DateTime.Now:HH:mm}";

                    ShowText(_lastForwardedText, token);
                }
                catch (Exception ex)
                {
                    Android.Util.Log.Warn(nameof(ForwardingService), ex.ToString());
                    ShowText($"Last check failed at {DateTime.Now:HH:mm}", token);
                }
            } while (await timer.WaitForNextTickAsync(token));
        }
        catch (System.OperationCanceledException)
        {
        }
    }

    private void ShowText(string text, CancellationToken token)
    {
        // Posting after the service was stopped would bring the notification back.
        if (token.IsCancellationRequested || text == _shownText)
            return;

        _shownText = text;
        ((NotificationManager?)GetSystemService(NotificationService))?.Notify(NotificationId, BuildNotification(text));
    }

    private Notification BuildNotification(string text)
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            var channel = new NotificationChannel(ChannelId, "Message forwarding", NotificationImportance.Low);
            ((NotificationManager?)GetSystemService(NotificationService))?.CreateNotificationChannel(channel);
        }

        var launchIntent = PackageManager?.GetLaunchIntentForPackage(PackageName ?? "");
        var openApp = launchIntent == null
            ? null
            : PendingIntent.GetActivity(this, 0, launchIntent, PendingIntentFlags.Immutable);

        return new NotificationCompat.Builder(this, ChannelId)
            .SetContentTitle("CommunicationApp")!
            .SetContentText(text)!
            .SetOnlyAlertOnce(true)!
            .SetSmallIcon(Android.Resource.Drawable.StatNotifySync)!
            .SetContentIntent(openApp)!
            .SetOngoing(true)!
            .Build()!;
    }
}
