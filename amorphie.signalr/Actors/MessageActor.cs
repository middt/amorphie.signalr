using Dapr.Actors.Runtime;
using Microsoft.AspNetCore.SignalR;
using amorphie.signalr.Models;
using amorphie.signalr.Database;
using Microsoft.EntityFrameworkCore;

namespace amorphie.signalr.Actors;

[Actor(TypeName = "MessageActor")]
public class MessageActor : Actor, IMessageActor
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<MessageActor> _logger;

    public MessageActor(
        ActorHost host,
        ApplicationDbContext context,
        IHubContext<NotificationHub> hubContext,
        ILogger<MessageActor> logger)
        : base(host)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<Message> SendMessageAsync(string userId, string content)
    {
        try
        {
            _logger.LogInformation("Actor {ActorId} sending message to user {UserId}", Id, userId);

            var message = new Message
            {
                Id = Id.ToString(),
                UserId = userId,
                Content = content,
                State = MessageState.Created,
                Timestamp = DateTime.UtcNow,
                RetryAttempts = 0,
                MaxRetryAttempts = 3,
                MessageTimeout = TimeSpan.FromHours(24)
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            if (NotificationHub.IsUserConnected(userId))
            {
                await _hubContext.Clients.User(userId)
                    .SendAsync("ReceiveMessage", message.Id, content);
                message.State = MessageState.Delivered;
                await _context.SaveChangesAsync();
                _logger.LogInformation("Message {MessageId} delivered to user {UserId}", message.Id, userId);
            }
            else
            {
                message.State = MessageState.Queued;
                await _context.SaveChangesAsync();
                _logger.LogInformation("User {UserId} not connected, message {MessageId} queued", userId, message.Id);
            }

            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message via actor {ActorId}", Id);
            throw;
        }
    }

    public async Task<bool> AcknowledgeMessageAsync(string messageId)
    {
        try
        {
            var message = await _context.Messages.FindAsync(messageId);
            if (message == null)
            {
                _logger.LogWarning("Message {MessageId} not found", messageId);
                return false;
            }

            if (message.IsAcknowledged)
            {
                return true;
            }

            message.State = MessageState.Acknowledged;
            message.AcknowledgedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            await UnregisterReminderAsync(messageId);

            _logger.LogInformation("Message {MessageId} acknowledged for user {UserId}", messageId, message.UserId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error acknowledging message {MessageId}", messageId);
            throw;
        }
    }

    public async Task<bool> RetryMessageAsync(string messageId)
    {
        try
        {
            var message = await _context.Messages.FindAsync(messageId);
            if (message == null || message.IsAcknowledged)
            {
                return true;
            }

            if (message.IsExpired)
            {
                message.State = MessageState.Expired;
                await _context.SaveChangesAsync();
                _logger.LogWarning("Message {MessageId} expired", messageId);
                return false;
            }

            if (NotificationHub.IsUserConnected(message.UserId))
            {
                await _hubContext.Clients.User(message.UserId)
                    .SendAsync("ReceiveMessage", messageId, message.Content);

                message.RetryAttempts++;
                message.State = MessageState.Delivered;
                await _context.SaveChangesAsync();

                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrying message {MessageId}", messageId);
            throw;
        }
    }

    public async Task<IEnumerable<Message>> GetUnacknowledgedMessagesAsync(string userId)
    {
        return await _context.Messages
            .Where(m => m.UserId == userId && !m.IsAcknowledged)
            .ToListAsync();
    }
}