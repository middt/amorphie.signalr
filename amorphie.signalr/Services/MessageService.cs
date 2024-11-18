using amorphie.signalr.Database;
using amorphie.signalr.Models;
using amorphie.signalr.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Dapr.Actors.Runtime;
using amorphie.signalr.Actors;
using Microsoft.AspNetCore.SignalR;
using Dapr.Actors;
using Dapr.Actors.Client;

public class MessageService : IMessageService
{
    private readonly IActorProxyFactory _actorProxyFactory;
    private readonly ILogger<MessageService> _logger;

    public MessageService(
        IActorProxyFactory actorProxyFactory,
        ILogger<MessageService> logger)
    {
        _actorProxyFactory = actorProxyFactory;
        _logger = logger;
    }

    public async Task<IEnumerable<Message>> GetUnacknowledgedMessagesAsync(string userId)
    {
        var actorId = new ActorId($"user-{userId}");
        var actor = _actorProxyFactory.CreateActorProxy<IMessageActor>(
            actorId,
            "MessageActor");

        return await actor.GetUnacknowledgedMessagesAsync(userId);
    }

    public async Task<bool> AcknowledgeMessageAsync(string messageId)
    {
        try
        {
            var actorId = new ActorId(messageId);
            var actor = _actorProxyFactory.CreateActorProxy<IMessageActor>(
                actorId,
                "MessageActor");

            _logger.LogInformation("Acknowledging message {MessageId}", messageId);
            return await actor.AcknowledgeMessageAsync(messageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging message {MessageId}", messageId);
            return false;
        }
    }

    public async Task<Message> SendMessageAsync(string userId, string content)
    {
        var actorId = new ActorId(Guid.NewGuid().ToString());
        var actor = _actorProxyFactory.CreateActorProxy<IMessageActor>(
            actorId,
            "MessageActor");

        return await actor.SendMessageAsync(userId, content);
    }
}