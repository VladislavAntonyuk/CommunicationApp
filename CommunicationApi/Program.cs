using CommunicationApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddScoped<SmsService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<TelegramService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/receive/{target}", async (IServiceProvider serviceProvider, IOptions<Recipient> options, [FromRoute] Target target, [FromBody] Message message) =>
    {
        using var scope = serviceProvider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<SmsService>();
        await sender.Send(options.Value.PhoneNumber, message.Text);
    }).WithDescription("This endpoint receives messages from external services and needs to notify Android client about new message.");

app.MapPost("/send", async (IServiceProvider serviceProvider, [FromBody] Message message) =>
    {
        var parts = message.Text.Split('|');
        var target = Enum.Parse<Target>(parts[0], true);
        using var scope = serviceProvider.CreateScope();
        switch (target)
        {
            case Target.Sms:
                var smsService = scope.ServiceProvider.GetRequiredService<SmsService>();
                await smsService.Send(parts[1], parts[2]);
                break;
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