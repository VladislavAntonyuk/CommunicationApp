using CommunicationApp.Services;
using CommunityToolkit.Maui;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Refit;

namespace CommunicationApp;

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
        builder.Services.AddRefitGeneratedClient<IGatewayApi>()
            .ConfigureHttpClient(client => client.BaseAddress = backend);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}