using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.SignalR;
using System.Linq;
using amorphie.signalr.Database;
using amorphie.signalr.Models;
using amorphie.signalr.Services;
using amorphie.signalr.Actors;
using Dapr.Client;
using Dapr.Actors.Runtime;
using Dapr.Actors;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseInMemoryDatabase("InMemoryDb"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

builder.Services.AddScoped<IMessageService, MessageService>();
//builder.Services.AddHostedService<MessageRetryService>();

// Add Dapr
builder.Services.AddDaprClient();
builder.Services.AddActors(options =>
{
    options.Actors.RegisterActor<MessageActor>();
});

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCloudEvents();
app.MapActorsHandlers();

// SignalR Hub
app.MapHub<NotificationHub>("/hubs/notification");

// Message endpoints
app.MapGet("/messages/{userId}", async (string userId, IMessageService messageService) =>
{
    var messages = await messageService.GetUnacknowledgedMessagesAsync(userId);
    return Results.Ok(messages);
})
.WithName("GetUnacknowledgedMessages")
.WithOpenApi();

app.MapPost("/messages/send", async (MessageRequest request, IMessageService messageService) =>
{
    var message = await messageService.SendMessageAsync(request.UserId, request.Content);
    return Results.Created($"/messages/{message.Id}", message);
})
.WithName("SendMessage")
.WithOpenApi();

app.MapPost("/messages/acknowledge/{messageId}", async (string messageId, IMessageService messageService) =>
{
    var result = await messageService.AcknowledgeMessageAsync(messageId);
    return result ? Results.Ok() : Results.NotFound();
})
.WithName("AcknowledgeMessage")
.WithOpenApi();

app.Run();

record MessageRequest(string UserId, string Content);