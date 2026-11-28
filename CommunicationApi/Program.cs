using CommunicationApi.Database;
using CommunicationApi.Services;
using CommunicationApi.Services.Auth;
using CommunicationApi.Services.Email;
using CommunicationApi.Services.Messaging;
using CommunicationApi.Services.Slack;
using CommunicationApi.Services.Teams;
using CommunicationApi.Services.Telegram;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Api;
using Silverback.Configuration;
using Silverback.Messaging.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.Configure<HostOptions>(opts =>
{
    opts.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore;
});

builder.Services.Configure<SlackSettings>(builder.Configuration.GetSection("Slack"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<MqttSettings>(builder.Configuration.GetSection("Mqtt"));
builder.Services.Configure<AuthSettings>(builder.Configuration.GetSection("Auth"));
builder.Services.AddScoped<LoginCodeService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddAuthentication(BearerTokenDefaults.AuthenticationScheme).AddBearerToken();
builder.Services.AddAuthorization();
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
builder.Services.AddScoped<OutgoingSmsSender>();
builder.Services.AddSingleton<TelegramLogins>();
builder.Services.AddScoped<ITelegramLogin>(sp => sp.GetRequiredService<TelegramLogins>().For(sp.GetRequiredService<ICurrentUser>().RequiredId));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/", () => {
    return "Created by DotNetChickUa. See demo video describing how to use the API: https://github.com/DotNetChickUa/CommunicationApp/blob/main/demo.mp4";
});

app.MapPost("/auth/code", async (LoginCodeService codes, HttpResponse response, LoginPhone request) =>
    {
        switch (await codes.SendCode(request.PhoneNumber))
        {
            case SendCodeResult.Sent sent:
                return Results.Ok(new LoginPhone(sent.PhoneNumber));
            case SendCodeResult.TooSoon tooSoon:
                response.Headers.RetryAfter = ((int)Math.Ceiling(tooSoon.RetryAfter.TotalSeconds)).ToString();
                return Results.Problem("Too many codes requested, try again later", statusCode: StatusCodes.Status429TooManyRequests);
            default:
                return Results.Problem("Invalid phone number", statusCode: StatusCodes.Status400BadRequest);
        }
    }).WithDescription("Sends a login code by SMS to the phone number.");

app.MapPost("/auth/login", async (LoginCodeService codes, LoginRequest request) =>
        await codes.CheckCode(request.PhoneNumber, request.Code) switch
        {
            CheckCodeResult.Valid valid => Results.SignIn(UserPrincipal.Create(valid.User), authenticationScheme: BearerTokenDefaults.AuthenticationScheme),
            CheckCodeResult.TooManyAttempts => Results.Problem("Too many wrong codes, request a new one", statusCode: StatusCodes.Status429TooManyRequests),
            CheckCodeResult.Invalid invalid => Results.Problem(invalid.Reason, statusCode: StatusCodes.Status400BadRequest),
            _ => throw new InvalidOperationException()
        })
    .WithDescription("Checks the login code and returns access and refresh tokens.");

app.MapPost("/auth/refresh", async (IOptionsMonitor<BearerTokenOptions> bearerOptions, CommunicationApiDbContext dbContext, RefreshRequest request) =>
    {
        var ticket = bearerOptions.Get(BearerTokenDefaults.AuthenticationScheme).RefreshTokenProtector.Unprotect(request.RefreshToken);
        if (ticket?.Properties.ExpiresUtc is not { } expiresUtc || DateTimeOffset.UtcNow >= expiresUtc || UserPrincipal.UserId(ticket.Principal) is not { } userId)
            return Results.Challenge();

        var user = await dbContext.Users.FindAsync(userId);
        return user == null
            ? Results.Challenge()
            : Results.SignIn(UserPrincipal.Create(user), authenticationScheme: BearerTokenDefaults.AuthenticationScheme);
    })
    .WithDescription("Exchanges a refresh token for new access and refresh tokens.");

var protectedApi = app.MapGroup("").RequireAuthorization();

protectedApi.MapGet("/providers",
    async (CommunicationApiDbContext dbContext, IConfiguration configuration) =>
    {
        var disabled = await dbContext.ProviderSettings
            .Where(x => !x.Enabled)
            .Select(x => x.Target)
            .ToListAsync();

        return ReceivingProviders.All.Select(x =>
            new ProviderStatus(x, !disabled.Contains(x), ReceivingProviders.IsConfigured(x, configuration)));
    }).WithDescription("Providers that can receive messages, and whether receiving is switched on for each.");

protectedApi.MapPut("/providers/{target}",
    async (CommunicationApiDbContext dbContext, ICurrentUser currentUser, Target target, ProviderSwitch request) =>
    {
        if (!ReceivingProviders.All.Contains(target))
            return Results.Problem($"{target} does not receive messages", statusCode: StatusCodes.Status400BadRequest);

        var setting = await dbContext.ProviderSettings.AsTracking().FirstOrDefaultAsync(x => x.Target == target);
        if (setting == null)
            dbContext.ProviderSettings.Add(new ProviderSetting { UserId = currentUser.RequiredId, Target = target, Enabled = request.Enabled });
        else
            setting.Enabled = request.Enabled;

        await dbContext.SaveChangesAsync();
        return Results.Ok();
    }).WithDescription("Switches receiving from a provider on or off.");

protectedApi.MapGet("/providers/{target}/chats",
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

protectedApi.MapPut("/providers/{target}/chats/{chatId}",
    async (CommunicationApiDbContext dbContext, ICurrentUser currentUser, Target target, string chatId, ProviderSwitch request) =>
    {
        if (target != Target.Telegram)
            return Results.Problem($"Chats cannot be chosen for {target}", statusCode: StatusCodes.Status400BadRequest);

        var setting = await dbContext.ChatSettings.AsTracking().FirstOrDefaultAsync(x => x.Target == target && x.ChatId == chatId);
        if (setting == null)
            dbContext.ChatSettings.Add(new ChatSetting { UserId = currentUser.RequiredId, Target = target, ChatId = chatId, Enabled = request.Enabled });
        else
            setting.Enabled = request.Enabled;

        await dbContext.SaveChangesAsync();
        return Results.Ok();
    }).WithDescription("Switches receiving from one chat on or off.");

protectedApi.MapGet("/telegram/login", (ITelegramLogin login, ILogger<Program> logger) => TelegramLoginResult(login.GetStatus, logger))
    .WithDescription("What is needed next to log in to Telegram: the phone, the code, the password, or nothing.");

protectedApi.MapPost("/telegram/login", (ITelegramLogin login, ILogger<Program> logger, TelegramLoginValue request) =>
        string.IsNullOrWhiteSpace(request.Value)
            ? Task.FromResult(Results.Problem("Value is required", statusCode: StatusCodes.Status400BadRequest))
            : TelegramLoginResult(() => login.Submit(request.Value), logger))
    .WithDescription("Takes the value the login is waiting for and returns what is needed next.");

protectedApi.MapPost("/telegram/logout", (ITelegramLogin login, ILogger<Program> logger) => TelegramLoginResult(login.Logout, logger))
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
