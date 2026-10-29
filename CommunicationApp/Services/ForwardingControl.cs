namespace CommunicationApp.Services;

public interface IForwardingControl
{
    void Start();
    void Stop();
}

public class ForwardingServiceControl : IForwardingControl
{
    public void Start() => ForwardingService.Start();

    public void Stop() => ForwardingService.Stop();
}
