using CommunicationClient.ViewModels;

namespace CommunicationClient.Pages;

public partial class ProvidersPage : ContentPage
{
    public ProvidersPage(ProvidersViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
