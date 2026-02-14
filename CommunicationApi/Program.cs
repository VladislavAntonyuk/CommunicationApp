using CommunicationApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.Configure<Recipient>(builder.Configuration.GetSection("Recipient"));
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
    async (CommunicationApiDbContext dbContext, [FromBody] Message message) =>
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
        return messages.Select(x=> new
        {
            x.Text
        });
    }).WithDescription(
    "This endpoint returns messages to Android client for back SMS.");

app.MapPost("/send", async (IServiceProvider serviceProvider, [FromBody] Message message) =>
{
    var parts = message.Text.Split('|');
    var target = Enum.Parse<Target>(parts[0], true);
    using var scope = serviceProvider.CreateScope();
    switch (target)
    {
        case Target.Email:
            var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
            await emailService.Send(parts[1], parts[2], parts[3]);
            break;
        case Target.Telegram:
            var telegramService = scope.ServiceProvider.GetRequiredService<TelegramService>();
            await telegramService.Send(parts[1], parts[2]);
            break;
        default:
            throw new ArgumentOutOfRangeException();
    }
}).WithDescription("Android client sends request to this endpoint.");

app.Run();

public class CommunicationApiDbContext(DbContextOptions<CommunicationApiDbContext> options) : DbContext(options)
{
    public DbSet<CommunicationApiMessage> Messages => Set<CommunicationApiMessage>();
}

public class CommunicationApiMessage
{
    public int Id { get; set; }
    public Target Target { get; set; }
    public required string Text { get; set; }
    public required DateTime DateTime { get; set; }
    public required bool IsRead { get; set; }
}