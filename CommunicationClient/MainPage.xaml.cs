namespace CommunicationClient;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        Backend.Text = Preferences.Get("backend", "");
    }

    private async void ProvidersButton_OnClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(ProvidersPage));
    }

    private void Backend_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        Preferences.Set("backend", Backend.Text);
    }
}
