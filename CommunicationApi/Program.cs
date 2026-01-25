using Microsoft.AspNetCore.Mvc;
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


app.MapGet("/receive", async (IServiceProvider serviceProvider, [FromBody] Message message) =>
    {
        using var scope = serviceProvider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredKeyedService<ISender>(message.Target);
        await sender.Send(message.Text);
    });

app.MapPost("/send", async (IServiceProvider serviceProvider, [FromBody] Message message) =>
    {
        using var scope = serviceProvider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredKeyedService<ISender>(message.Target);
        await sender.Send(message.Text);
    });

app.Run();

interface ISender
{
    Task Send(string text);
}
class SmsService:ISender
{
    public Task Send(string text)
    {
        return Task.CompletedTask;
    }
}
class EmailService:ISender
{
    public Task Send(string text)
    {
        return Task.CompletedTask;
    }
}
class TelegramService:ISender
{
    public Task Send(string text)
    {
        return Task.CompletedTask;
    }
}