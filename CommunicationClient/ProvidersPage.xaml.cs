using CommunicationClient.ViewModels;

namespace CommunicationClient;

public partial class ProvidersPage : ContentPage
{
    public ProvidersPage(ProvidersViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
