using CommunicationClient.ViewModels;

namespace CommunicationClient.Pages;

public partial class TelegramLoginPage : ContentPage
{
    public TelegramLoginPage(TelegramLoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
