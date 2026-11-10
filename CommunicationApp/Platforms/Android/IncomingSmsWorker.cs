using Android.Content;
using Android.Runtime;
using Android.Util;
using AndroidX.Work;
using CommunicationApp.Services;

namespace CommunicationApp;

[Register("com.companyname.communicationapp.IncomingSmsWorker")]
public sealed class IncomingSmsWorker(Context context, WorkerParameters parameters) : Worker(context, parameters)
{
    private const string WorkName = "incoming-sms";
    private const string IdKey = "id";
    private const string TextKey = "text";
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(30);

    private readonly CancellationTokenSource _stopped = new();

    public static void Enqueue(Context context, string text)
    {
        var request = new OneTimeWorkRequest.Builder(Java.Lang.Class.FromType(typeof(IncomingSmsWorker)))
            .SetInputData(new Data.Builder()
                .PutString(IdKey, Guid.NewGuid().ToString("N"))!
                .PutString(TextKey, text)!
                .Build())
            .SetConstraints(new Constraints.Builder()
                .SetRequiredNetworkType(NetworkType.Connected!)
                .Build())
            .Build();

        WorkManager.GetInstance(context)!.EnqueueUniqueWork(WorkName, ExistingWorkPolicy.AppendOrReplace!, (OneTimeWorkRequest)request);
    }

    public override Result DoWork()
    {
        var id = InputData!.GetString(IdKey);
        var text = InputData.GetString(TextKey);
        if (id == null || text == null)
            return Result.InvokeFailure()!;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_stopped.Token);
            timeout.CancelAfter(PublishTimeout);
            var forwarder = IPlatformApplication.Current!.Services.GetRequiredService<IncomingSmsForwarder>();
            forwarder.Forward(text, id, timeout.Token).GetAwaiter().GetResult();
            return Result.InvokeSuccess()!;
        }
        catch (Exception ex)
        {
            Log.Warn(nameof(IncomingSmsWorker), $"Could not forward the incoming SMS, will retry: {ex}");
            return Result.InvokeRetry()!;
        }
    }

    public override void OnStopped()
    {
        _stopped.Cancel();
        base.OnStopped();
    }
}
