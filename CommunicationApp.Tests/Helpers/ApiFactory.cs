using CommunicationApi.Database;
using CommunicationApi.Services;
using CommunicationApi.Services.Auth;
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
using Shared.Api;
using Silverback.Messaging.Publishing;

namespace CommunicationApp.Tests.Helpers;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString = $"Data Source={Guid.NewGuid():N};Mode=Memory;Cache=Shared";
    private readonly SqliteConnection _connection;

    public ApiFactory()
    {
        _connection = new SqliteConnection(_connectionString);
    }

    public const string ClientPhoneNumber = "+380501234567";
    public const string OtherPhoneNumber = "+380671234567";

    public FakeSenders Senders { get; } = new();

    public FakeTelegramLogin TelegramLogin { get; } = new();

    public FakeMessagePublisher Publisher { get; } = new();

    public async Task SendSms(Message message, string phoneNumber = ClientPhoneNumber)
    {
        var user = TestTokens.SaveUser(Services, phoneNumber);
        await using var scope = Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ICurrentUser>().Set(user.Id);
        await scope.ServiceProvider.GetRequiredService<OutgoingSmsSender>().Send(Target.Telegram, message);
    }

    protected virtual void ConfigureMessaging(IServiceCollection services)
    {
        services.RemoveAll<IHostedService>();
        services.AddSingleton<IPublisher>(Publisher);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseSetting("Auth:AllowedRegions:0", "UA");
        builder.UseSetting("Auth:AllowedRegions:1", "US");

        builder.ConfigureTestServices(services =>
        {
            ConfigureMessaging(services);

            services.RemoveAll<IDbContextOptionsConfiguration<CommunicationApiDbContext>>();
            services.RemoveAll<DbContextOptions<CommunicationApiDbContext>>();
            services.AddDbContext<CommunicationApiDbContext>(s => s.UseSqlite(_connectionString));

            services.AddSingleton<IEmailService>(Senders);
            services.AddScoped(sp => Senders.ForUser(sp.GetRequiredService<ICurrentUser>()));
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
