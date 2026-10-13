using CommunicationApi.Database;
using CommunicationApi.Services.Email;
using CommunicationApi.Services.Slack;
using CommunicationApi.Services.Teams;
using CommunicationApi.Services.Telegram;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CommunicationApi.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public FakeSenders Senders { get; } = new();

    public FakeTelegramLogin TelegramLogin { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.ConfigureTestServices(services =>
        {
            var listeners = services
                .Where(x => x.ServiceType == typeof(IHostedService)
                            && (x.ImplementationType == typeof(TelegramBackgroundService)
                                || x.ImplementationType == typeof(SlackBackgroundService)))
                .ToList();
            foreach (var listener in listeners)
            {
                services.Remove(listener);
            }

            services.RemoveAll<IDbContextOptionsConfiguration<CommunicationApiDbContext>>();
            services.RemoveAll<DbContextOptions<CommunicationApiDbContext>>();
            services.AddDbContext<CommunicationApiDbContext>(s => s.UseSqlite(_connection));

            services.AddSingleton<IEmailService>(Senders);
            services.AddSingleton<ITelegramService>(Senders);
            services.AddSingleton<ITeamsService>(Senders);
            services.AddSingleton<ISlackService>(Senders);
            services.AddSingleton<ITelegramLogin>(TelegramLogin);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}
