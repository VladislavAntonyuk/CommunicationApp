using Shared;

namespace CommunicationApp.Services;

public sealed class MessageForwarder(IGatewayApi api, ISmsSender smsSender, ForwardingStatus status, IPreferences preferences)
{
    // The button and the background service must not forward at the same time, or the same message is sent twice.
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<ForwardResult> ForwardPending()
    {
        await _gate.WaitAsync();
        try
        {
            var result = await Forward();
            if (result.Error == null)
                status.Succeeded(result.Sent);
            else
                status.Failed(result.Sent, result.Error);
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ForwardResult> Forward()
    {
        var phoneNumber = preferences.Get("from", "");

        var messages = await api.GetPending();
        if (!messages.IsSuccessfulWithContent)
            return new ForwardResult(0, messages.ErrorMessage);

        var sent = 0;
        foreach (var item in messages.Content)
        {
            try
            {
                smsSender.Send(phoneNumber, item.Text);
            }
            catch (Exception ex)
            {
                return new ForwardResult(sent, $"Could not send SMS: {ex.Message}");
            }

            var response = await api.Acknowledge(new Acknowledgement([item.Id]));
            if (!response.IsSuccessful)
                return new ForwardResult(sent, response.ErrorMessage);
            sent++;
        }

        return new ForwardResult(sent, null);
    }
}
