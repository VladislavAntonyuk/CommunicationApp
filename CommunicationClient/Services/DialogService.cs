namespace CommunicationClient.Services;

public interface IDialogService
{
    Task<bool> Confirm(string title, string message, string accept, string cancel);
}

public class ShellDialogService : IDialogService
{
    public Task<bool> Confirm(string title, string message, string accept, string cancel) =>
        Shell.Current.DisplayAlertAsync(title, message, accept, cancel);
}
