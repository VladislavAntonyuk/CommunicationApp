using CommunicationClient.Pages;
using CommunicationClient.Services;

namespace CommunicationClient;

public partial class AppShell : Shell
{
    public AppShell(AuthSession session)
    {
        InitializeComponent();
        session.LoggedOut += () => MainThread.BeginInvokeOnMainThread(async () => await GoToAsync($"//{nameof(ProvidersPage)}/{nameof(LoginPage)}"));
    }
}
