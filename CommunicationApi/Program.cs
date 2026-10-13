using CommunicationApi.Database;
using CommunicationApi.Services;
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
builder.Services.AddDbContext<CommunicationApiDbContext>(s => s.UseSqlite(builder.Configuration.GetConnectionString("CommunicationApiDb")));
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITelegramService, TelegramService>();
builder.Services.AddScoped<ISlackService, SlackService>();
builder.Services.AddScoped<ITeamsService, TeamsService>();
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
            .Where(x => !x.IsRead)
            .OrderBy(x => x.DateTime)
            .ToListAsync();

        return messages.Select(x => new ReceivedMessage(x.Id, x.Text));
    }).WithDescription(
    "This endpoint returns messages to Android client for back SMS. Messages are returned until they are acknowledged.");

app.MapPost("/receive/ack",
    async (CommunicationApiDbContext dbContext, Acknowledgement request) =>
    {
        var messages = await dbContext.Messages
            .AsTracking()
            .Where(x => !x.IsRead && request.Ids.Contains(x.Id))
            .ToListAsync();
        foreach (var item in messages)
        {
            item.IsRead = true;
        }

        await dbContext.SaveChangesAsync();
    }).WithDescription(
    "Android client confirms the messages it has sent as SMS, so they are not returned again.");

app.MapPost("/send", async (IServiceProvider serviceProvider, ILogger<Program> logger, Shared.Message message) =>
{
    if (string.IsNullOrWhiteSpace(message.Text))
        return Results.Problem("Text is required", statusCode: StatusCodes.Status400BadRequest);

    var targetName = message.Text.Split('|', 2)[0].Trim();
    if (!Enum.GetNames<Target>().Contains(targetName, StringComparer.OrdinalIgnoreCase))
        return Results.Problem($"Unknown target '{targetName}'", statusCode: StatusCodes.Status400BadRequest);

    var target = Enum.Parse<Target>(targetName, true);
    var fieldCount = target == Target.Email ? 4 : 3;
    
    var parts = message.Text.Split('|', fieldCount);
    if (parts.Length < fieldCount)
        return Results.Problem(
            target == Target.Email ? "Expected Email|to|subject|text" : $"Expected {target}|recipient|text",
            statusCode: StatusCodes.Status400BadRequest);

    if (target == Target.Telegram && !long.TryParse(parts[1], out _))
        return Results.Problem("Telegram recipient must be a numeric chat id", statusCode: StatusCodes.Status400BadRequest);

    using var scope = serviceProvider.CreateScope();
    try
    {
        switch (target)
        {
            case Target.Email:
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                var status = await emailService.Send(parts[1], parts[2], parts[3]);
                return string.Equals(status, "success", StringComparison.OrdinalIgnoreCase)
                    ? Results.Ok(status)
                    : Results.Problem($"Email is not sent: {status}", statusCode: StatusCodes.Status502BadGateway);
            case Target.Telegram:
                var telegramService = scope.ServiceProvider.GetRequiredService<ITelegramService>();
                await telegramService.Send(parts[1], parts[2]);
                return Results.Ok();
            case Target.Teams:
                var teamsService = scope.ServiceProvider.GetRequiredService<ITeamsService>();
                await teamsService.Send(parts[1], parts[2]);
                return Results.Ok();
            case Target.Slack:
                var slackService = scope.ServiceProvider.GetRequiredService<ISlackService>();
                await slackService.Send(parts[1], parts[2]);
                return Results.Ok();
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to send {Target} message", target);
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
}).WithDescription("Android client sends request to this endpoint.");

app.MapPost("/notify/{target}", async (CommunicationApiDbContext dbContext, Target target, Shared.Message request) =>
{
    if (await dbContext.ProviderSettings.AnyAsync(x => x.Target == target && !x.Enabled))
        return;

    dbContext.Messages.Add(new CommunicationApiMessage
    {
        Text = request.Text,
        DateTime = DateTime.UtcNow,
        IsRead = false,
        Target = target
    });
    await dbContext.SaveChangesAsync();
}).WithDescription("Service sends webhooks on this endpoint.");

app.MapGet("/providers",
    async (CommunicationApiDbContext dbContext, IConfiguration configuration) =>
    {
        var disabled = await dbContext.ProviderSettings
            .Where(x => !x.Enabled)
            .Select(x => x.Target)
            .ToListAsync();

        return ReceivingProviders.All.Select(x =>
            new ProviderStatus(x, !disabled.Contains(x), ReceivingProviders.IsConfigured(x, configuration)));
    }).WithDescription("Providers that can receive messages, and whether receiving is switched on for each.");

app.MapPut("/providers/{target}",
    async (CommunicationApiDbContext dbContext, Target target, ProviderSwitch request) =>
    {
        if (!ReceivingProviders.All.Contains(target))
            return Results.Problem($"{target} does not receive messages", statusCode: StatusCodes.Status400BadRequest);

        var setting = await dbContext.ProviderSettings.AsTracking().FirstOrDefaultAsync(x => x.Target == target);
        if (setting == null)
            dbContext.ProviderSettings.Add(new ProviderSetting { Target = target, Enabled = request.Enabled });
        else
            setting.Enabled = request.Enabled;

        await dbContext.SaveChangesAsync();
        return Results.Ok();
    }).WithDescription("Switches receiving from a provider on or off.");

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
if (dbContext.Database.IsRelational())
    dbContext.Database.Migrate();
else
    dbContext.Database.EnsureCreated();

app.Run();

public class TelegramAuthModel
{
    public required string Code { get; set; }
}
