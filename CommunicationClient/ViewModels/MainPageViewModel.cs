using CommunicationClient.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommunicationClient.ViewModels;

public partial class MainPageViewModel(IPreferences preferences, INavigationService navigation) : ObservableObject
{
    [ObservableProperty]
    public partial string Backend { get; set; } = preferences.Get(BackendAddress.Key, "");

    partial void OnBackendChanged(string value) => preferences.Set(BackendAddress.Key, value);

    [RelayCommand]
    private Task OpenProviders() => navigation.GoTo(nameof(ProvidersPage));
}
