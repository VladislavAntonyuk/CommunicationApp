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

    private CancellationTokenSource? _cts;

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
        ServiceCompat.StartForeground(this, NotificationId, BuildNotification(), (int)ForegroundService.TypeSpecialUse);

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

    private static async Task Run(CancellationToken token)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            do
            {
                try
                {
                    await MessageForwarder.ForwardPending();
                }
                catch (Exception ex)
                {
                    Android.Util.Log.Warn(nameof(ForwardingService), ex.ToString());
                }
            } while (await timer.WaitForNextTickAsync(token));
        }
        catch (System.OperationCanceledException)
        {
        }
    }

    private Notification BuildNotification()
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
            .SetContentText("Forwarding messages")!
            .SetSmallIcon(Android.Resource.Drawable.StatNotifySync)!
            .SetContentIntent(openApp)!
            .SetOngoing(true)!
            .Build()!;
    }
}
