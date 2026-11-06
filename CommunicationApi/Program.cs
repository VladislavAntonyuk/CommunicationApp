using CommunicationApi.Database;
using CommunicationApi.Services;
using CommunicationApi.Services.Email;
using CommunicationApi.Services.Messaging;
using CommunicationApi.Services.Slack;
using CommunicationApi.Services.Teams;
using CommunicationApi.Services.Telegram;
using Microsoft.EntityFrameworkCore;
using Shared;
using Silverback.Configuration;
using Silverback.Messaging.Configuration;
using Silverback.Messaging.Publishing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.Configure<HostOptions>(opts =>
{
    opts.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

builder.Services.Configure<SlackSettings>(builder.Configuration.GetSection("Slack"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<MqttSettings>(builder.Configuration.GetSection("Mqtt"));
builder.Services.AddDbContext<CommunicationApiDbContext>(s => s.UseSqlite(builder.Configuration.GetConnectionString("CommunicationApiDb")));
builder.Services.AddSilverback()
    .WithConnectionToMessageBroker(options => options.AddMqtt().ConnectAfterStartup().RetryOnConnectionFailure())
    .AddBrokerClientsConfigurator<BrokerClientsConfigurator>()
    .AddScopedSubscriber<SmsAckSubscriber>()
    .AddScopedSubscriber<IncomingSmsSubscriber>()
    .AddScopedErrorPolicyCallback<IncomingSmsSkipRecorder>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<ITelegramService, TelegramService>();
builder.Services.AddScoped<ISlackService, SlackService>();
builder.Services.AddScoped<ITeamsService, TeamsService>();
builder.Services.AddScoped<IncomingSmsRouter>();
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

app.MapPost("/send", async (IncomingSmsRouter router, ILogger<Program> logger, Shared.Message message) =>
{
    try
    {
        await router.Send(message.Text);
        return Results.Ok();
    }
    catch (InvalidSmsException ex)
    {
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status400BadRequest);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to send message");
        return Results.Problem(ex.Message, statusCode: StatusCodes.Status502BadGateway);
    }
}).WithDescription("Android client sends request to this endpoint.");

app.MapPost("/notify/{target}", async (CommunicationApiDbContext dbContext, IPublisher publisher, Target target, Shared.Message request) =>
{
    if (await dbContext.ProviderSettings.AnyAsync(x => x.Target == target && !x.Enabled))
        return;

    if (request.ChatId != null)
    {
        var saved = await dbContext.ChatSettings.FirstOrDefaultAsync(x => x.Target == target && x.ChatId == request.ChatId);
        if (!(saved?.Enabled ?? ChatDefaults.IsEnabled(request.ChatKind)))
            return;
    }

    var id = Guid.NewGuid().ToString("N");
    dbContext.OutgoingMessages.Add(new OutgoingMessage { Id = id, CreatedAt = DateTime.UtcNow });
    await dbContext.SaveChangesAsync();

    await publisher.PublishAsync(new OutgoingSms(request.Text, id));
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
