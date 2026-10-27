using CommunicationClient.ViewModels;

namespace CommunicationClient;

public partial class TelegramLoginPage : ContentPage
{
    private readonly TelegramLoginViewModel _viewModel;

    public TelegramLoginPage(TelegramLoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }
}
