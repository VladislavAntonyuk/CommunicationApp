using CommunicationApi.Database;
using CommunicationApi.Services.Email;
using CommunicationApi.Services.Slack;
using CommunicationApi.Services.Teams;
using CommunicationApi.Services.Telegram;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CommunicationApi.Tests;

public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = Guid.NewGuid().ToString();

    public FakeSenders Senders { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
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
            services.AddDbContext<CommunicationApiDbContext>(s => s.UseInMemoryDatabase(_databaseName));

            services.AddSingleton<IEmailService>(Senders);
            services.AddSingleton<ITelegramService>(Senders);
            services.AddSingleton<ITeamsService>(Senders);
            services.AddSingleton<ISlackService>(Senders);
        });
    }
}
