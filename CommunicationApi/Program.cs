using CommunicationApi.Database;
using CommunicationApi.Services;
using CommunicationApi.Services.Email;
using CommunicationApi.Services.Slack;
using CommunicationApi.Services.Teams;
using CommunicationApi.Services.Telegram;
using Microsoft.EntityFrameworkCore;
using Shared;

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
builder.Services.AddSingleton<TelegramLogin>();
builder.Services.AddSingleton<ITelegramLogin>(sp => sp.GetRequiredService<TelegramLogin>());

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

    if (request.ChatId != null)
    {
        var saved = await dbContext.ChatSettings.FirstOrDefaultAsync(x => x.Target == target && x.ChatId == request.ChatId);
        if (!(saved?.Enabled ?? ChatDefaults.IsEnabled(request.ChatKind)))
            return;
    }

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

app.MapGet("/providers/{target}/chats",
    async (CommunicationApiDbContext dbContext, ITelegramService telegramService, ILogger<Program> logger, Target target) =>
    {
        if (target != Target.Telegram)
            return Results.Problem($"Chats cannot be chosen for {target}", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var chats = await telegramService.GetChats();
            var saved = await dbContext.ChatSettings
                .Where(x => x.Target == target)
                .ToDictionaryAsync(x => x.ChatId, x => x.Enabled);

            return Results.Ok(chats.Select(x =>
                new ChatStatus(x.Id, x.Title, x.Kind, saved.TryGetValue(x.Id, out var enabled) ? enabled : ChatDefaults.IsEnabled(x.Kind))));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get {Target} chats", target);
            return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
        }
    }).WithDescription("Chats of a provider, and whether messages from each are received.");

app.MapPut("/providers/{target}/chats/{chatId}",
    async (CommunicationApiDbContext dbContext, Target target, string chatId, ProviderSwitch request) =>
    {
        if (target != Target.Telegram)
            return Results.Problem($"Chats cannot be chosen for {target}", statusCode: StatusCodes.Status400BadRequest);

        var setting = await dbContext.ChatSettings.AsTracking().FirstOrDefaultAsync(x => x.Target == target && x.ChatId == chatId);
        if (setting == null)
            dbContext.ChatSettings.Add(new ChatSetting { Target = target, ChatId = chatId, Enabled = request.Enabled });
        else
            setting.Enabled = request.Enabled;

        await dbContext.SaveChangesAsync();
        return Results.Ok();
    }).WithDescription("Switches receiving from one chat on or off.");

app.MapGet("/telegram/login", (ITelegramLogin login, ILogger<Program> logger) => TelegramLoginResult(login.GetStatus, logger))
    .WithDescription("What is needed next to log in to Telegram: the phone, the code, the password, or nothing.");

app.MapPost("/telegram/login", (ITelegramLogin login, ILogger<Program> logger, TelegramLoginValue request) =>
        string.IsNullOrWhiteSpace(request.Value)
            ? Task.FromResult(Results.Problem("Value is required", statusCode: StatusCodes.Status400BadRequest))
            : TelegramLoginResult(() => login.Submit(request.Value), logger))
    .WithDescription("Takes the value the login is waiting for and returns what is needed next.");

app.MapPost("/telegram/logout", (ITelegramLogin login, ILogger<Program> logger) => TelegramLoginResult(login.Logout, logger))
    .WithDescription("Logs out of Telegram, or abandons a login that is waiting for a code or password.");

using var scope = app.Services.CreateScope();
await using var dbContext = scope.ServiceProvider.GetRequiredService<CommunicationApiDbContext>();
if (dbContext.Database.IsRelational())
    dbContext.Database.Migrate();
else
    dbContext.Database.EnsureCreated();

app.Run();

static async Task<IResult> TelegramLoginResult(Func<Task<TelegramLoginStatus>> action, ILogger logger)
{
    try
    {
        return Results.Ok(await action());
    }
    catch (TelegramLoginException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Telegram login request failed");
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
}
