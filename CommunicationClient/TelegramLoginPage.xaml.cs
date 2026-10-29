using CommunicationClient.ViewModels;

namespace CommunicationClient;

public partial class TelegramLoginPage : ContentPage
{
    public TelegramLoginPage(TelegramLoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
