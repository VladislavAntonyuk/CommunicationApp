using CommunicationClient.ViewModels;

namespace CommunicationClient.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    protected override bool OnBackButtonPressed()
    {
        Platform.CurrentActivity?.MoveTaskToBack(true);
        return true;
    }
}
