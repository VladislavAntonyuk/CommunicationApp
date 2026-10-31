using CommunityToolkit.Maui.Alerts;

namespace CommunicationApp.Services;

public interface INotifier
{
    Task Show(string text);
}

public class ToastNotifier : INotifier
{
    public Task Show(string text) => Toast.Make(text).Show();
}
