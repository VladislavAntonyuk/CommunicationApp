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

public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly string _queueName = $"test-{Guid.NewGuid():N}";

    public FakeSenders Senders { get; } = new();

    public FakeTelegramLogin TelegramLogin { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();
        await ServiceBusFixture.Administration.CreateQueueAsync(_queueName);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _connection.DisposeAsync();
        await ServiceBusFixture.Administration.DeleteQueueAsync(_queueName);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:MessageQueue", ServiceBusFixture.ConnectionString);
        builder.UseSetting("MessageQueue:Name", _queueName);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IHostedService>();

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
}
