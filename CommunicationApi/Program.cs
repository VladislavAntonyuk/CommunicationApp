using CommunicationApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddKeyedScoped<ISender, SmsService>(Target.Sms);
builder.Services.AddKeyedScoped<ISender, EmailService>(Target.Email);
builder.Services.AddKeyedScoped<ISender, TelegramService>(Target.Telegram);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapPost("/receive/{target}", async (IServiceProvider serviceProvider, IOptions<Recipient> options, [FromRoute] Target target, [FromBody] Message message) =>
    {
        using var scope = serviceProvider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredKeyedService<ISender>(Target.Sms);
        await sender.Send(options.Value.PhoneNumber, message.MessageText);
    }).WithDescription("This endpoint receives messages from external services and needs to notify Android client about new message.");

app.MapPost("/send", async (IServiceProvider serviceProvider, [FromBody] Message message) =>
    {
        using var scope = serviceProvider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredKeyedService<ISender>(message.Target);
        await sender.Send(message.Recipient, message.MessageText);
    }).WithDescription("Android client sends request to this endpoint.");

app.Run();