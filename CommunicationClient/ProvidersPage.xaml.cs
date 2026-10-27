using CommunicationClient.ViewModels;

namespace CommunicationClient;

public partial class ProvidersPage : ContentPage
{
    private readonly ProvidersViewModel _viewModel;

    public ProvidersPage(ProvidersViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void Provider_OnToggled(object? sender, ToggledEventArgs e)
    {
        if (sender is Switch { BindingContext: ProviderRow row })
            await _viewModel.SetProvider(row, e.Value);
    }

    private async void Chat_OnToggled(object? sender, ToggledEventArgs e)
    {
        if (sender is Switch { BindingContext: ChatRow row })
            await _viewModel.SetChat(row, e.Value);
    }
}
