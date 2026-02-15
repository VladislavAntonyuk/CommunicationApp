using CommunicationApi.Database;
using CommunicationApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared;
using TL;
using WTelegram;
using Message = Shared.Message;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddDbContext<CommunicationApiDbContext>(s => s.UseInMemoryDatabase("CommunicationApiDb"));
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<TelegramService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

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

app.MapPost("/send", async (IServiceProvider serviceProvider, [FromBody] Shared.Message message) =>
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
        default:
            throw new ArgumentOutOfRangeException();
    }
}).WithDescription("Android client sends request to this endpoint.");

app.MapPost("/notify/{target}", async (CommunicationApiDbContext dbContext, Target target, [FromBody] Message request) =>
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

using var scope = app.Services.CreateScope();
await using var dbContext = scope.ServiceProvider.GetRequiredService<CommunicationApiDbContext>();
dbContext.Database.EnsureCreated();

var telegram = app.Configuration.GetSection("Telegram").Get<TelegramSettings>();
await using var client = new Client((what) => what switch
{
    "api_id" => telegram.AppId,
    "api_hash" => telegram.AppHash,
    "phone_number" => telegram.Phone,
    _ => null
});

var me = await client.LoginUserIfNeeded();
Console.WriteLine($"Logged in as {me.username ?? me.first_name}");
client.OnUpdates += async (updates) =>
{
    foreach (var u in updates.UpdateList)
    {
        if (u is UpdateNewMessage { message: TL.Message mb } && mb.From.ID != me.ID)
        {
            using var http = new HttpClient();

            var payload = new Message($"Telegram|{mb.Peer.ID}|{mb.message}");

            await http.PostAsJsonAsync($"/notify/{Target.Telegram}", payload);
        }
    }
};
app.Run();