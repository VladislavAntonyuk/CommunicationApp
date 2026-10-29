namespace CommunicationClient.Services;

public interface INavigationService
{
    Task GoTo(string route);
    Task GoBack();
}

public class ShellNavigationService : INavigationService
{
    public Task GoTo(string route) => Shell.Current.GoToAsync(route);

    public Task GoBack() => Shell.Current.GoToAsync("..");
}
