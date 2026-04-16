using CommunicationApi.Database;
using CommunicationApi.Services.Email;
using CommunicationApi.Services.Slack;
using CommunicationApi.Services.Teams;
using CommunicationApi.Services.Telegram;
using Microsoft.EntityFrameworkCore;
using Shared;
using WTelegram;

string? _password = null;
string? _otp = null;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.Configure<HostOptions>(opts =>
{
    opts.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

builder.Services.Configure<SlackSettings>(builder.Configuration.GetSection("Slack"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddDbContext<CommunicationApiDbContext>(s => s.UseInMemoryDatabase("CommunicationApiDb"));
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<TelegramService>();
builder.Services.AddScoped<SlackService>();
builder.Services.AddScoped<TeamsService>();
builder.Services.AddHostedService<TelegramBackgroundService>();
builder.Services.AddHostedService<SlackBackgroundService>();
builder.Services.AddSingleton<Client>(sp =>
{
    var telegram = builder.Configuration.GetSection("Telegram").Get<TelegramSettings>();

    string ConfigProvider(string what)
    {
        switch (what)
        {
            case "api_id":
                return telegram.AppId;
            case "api_hash":
                return telegram.AppHash;
            case "phone_number":
                return telegram.Phone;
            case "session_pathname":
                return Path.Combine(AppContext.BaseDirectory, "telegram.session");
            case "verification_code":
                while (_otp == null)
                    Thread.Sleep(1000);

                return _otp;
            case "password":
                while (_password == null)
                    Thread.Sleep(1000);

                return _password;
            default:
                return null;
        }
    }

    var client = new Client(ConfigProvider);
    return client;
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/", () => {
    return "Created by DotNetChickUa. See demo video describing how to use the API: https://github.com/DotNetChickUa/CommunicationApp/blob/main/demo.mp4";
});

app.MapGet("/receive",
    async (CommunicationApiDbContext dbContext) =>
    {
        var messages = await dbContext.Messages
            .AsTracking()
            .Where(x => !x.IsRead)
            .OrderBy(x => x.DateTime)
            .ToListAsync();
        foreach (var item in messages)
        {
            item.IsRead = true;
        }

        await dbContext.SaveChangesAsync();
        return messages.Select(x => new
        {
            x.Text
        });
    }).WithDescription(
    "This endpoint returns messages to Android client for back SMS.");

app.MapPost("/send", async (IServiceProvider serviceProvider, Shared.Message message) =>
{
    var parts = message.Text.Split('|');
    var target = Enum.Parse<Target>(parts[0], true);
    using var scope = serviceProvider.CreateScope();
    switch (target)
    {
        case Target.Email:
            var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
            return Results.Ok(await emailService.Send(parts[1], parts[2], parts[3]));
        case Target.Telegram:
            var telegramService = scope.ServiceProvider.GetRequiredService<TelegramService>();
            await telegramService.Send(parts[1], parts[2]);
            return Results.Ok();
        case Target.Teams:
            var teamsService = scope.ServiceProvider.GetRequiredService<TeamsService>();
            await teamsService.Send(parts[1], parts[2]);
            return Results.Ok();
        case Target.Slack:
            var slackService = scope.ServiceProvider.GetRequiredService<SlackService>();
            await slackService.Send(parts[1], parts[2]);
            return Results.Ok();
        default:
            throw new ArgumentOutOfRangeException();
    }
}).WithDescription("Android client sends request to this endpoint.");

app.MapPost("/notify/{target}", async (CommunicationApiDbContext dbContext, Target target, Shared.Message request) =>
{
    dbContext.Messages.Add(new CommunicationApiMessage
    {
        Text = request.Text,
        DateTime = DateTime.UtcNow,
        IsRead = false,
        Target = target
    });
    await dbContext.SaveChangesAsync();
}).WithDescription("Service sends webhooks on this endpoint.");

app.MapPost("/telegram/otp", (TelegramAuthModel code) =>
{
    _otp = code.Code;
});

app.MapPost("/telegram/password", (TelegramAuthModel code) =>
{
    _password = code.Code;
});

using var scope = app.Services.CreateScope();
await using var dbContext = scope.ServiceProvider.GetRequiredService<CommunicationApiDbContext>();
dbContext.Database.EnsureCreated();

app.Run();

public class TelegramAuthModel
{
    public required string Code { get; set; }
}
