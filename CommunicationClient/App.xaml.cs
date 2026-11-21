using CommunicationClient.Services;

namespace CommunicationClient;

public partial class App : Application
{
    private readonly AuthSession _session;

    public App(AuthSession session)
    {
        InitializeComponent();
        _session = session;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(new AppShell(_session));
    }
}
