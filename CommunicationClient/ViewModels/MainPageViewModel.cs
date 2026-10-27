using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace CommunicationClient.ViewModels;

public partial class MainPageViewModel(IPreferences preferences) : ObservableObject
{
    [ObservableProperty]
    public partial string Backend { get; set; } = preferences.Get("backend", "");

    partial void OnBackendChanged(string value) => preferences.Set("backend", value);

    [RelayCommand]
    private Task OpenProviders() => Shell.Current.GoToAsync(nameof(ProvidersPage));
}
