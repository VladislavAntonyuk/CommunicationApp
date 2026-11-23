using CommunicationClient.Pages;
using CommunicationClient.Services;
using CommunicationClient.ViewModels;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Refit;

namespace CommunicationClient;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        foreach (var name in new[] { "appsettings.json", "appsettings.Development.json" })
        {
            var settings = typeof(MauiProgram).Assembly.GetManifestResourceStream(name);
            if (settings != null)
                builder.Configuration.AddJsonStream(settings);
        }

        var backend = new Uri(builder.Configuration["Backend"]!);
        builder.Services.AddSingleton(SecureStorage.Default);
        builder.Services.AddSingleton<ITokenStorage, SecureTokenStorage>();
        builder.Services.AddSingleton<AuthSession>();
        builder.Services.AddTransient<AuthHandler>();
        builder.Services.AddRefitGeneratedClient<IAuthApi>()
            .ConfigureHttpClient(client => client.BaseAddress = backend);
        builder.Services.AddRefitGeneratedClient<ICommunicationApi>()
            .ConfigureHttpClient(client => client.BaseAddress = backend)
            .AddHttpMessageHandler<AuthHandler>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<IDialogService, ShellDialogService>();
        builder.Services.AddTransientWithShellRoute<LoginPage, LoginViewModel>(nameof(LoginPage));
        builder.Services.AddTransient<ProvidersPage, ProvidersViewModel>();
        builder.Services.AddTransientWithShellRoute<TelegramLoginPage, TelegramLoginViewModel>(nameof(TelegramLoginPage));

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}