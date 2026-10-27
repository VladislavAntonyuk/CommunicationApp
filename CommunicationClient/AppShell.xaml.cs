namespace CommunicationClient;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute(nameof(ProvidersPage), typeof(ProvidersPage));
        Routing.RegisterRoute(nameof(TelegramLoginPage), typeof(TelegramLoginPage));
    }
}