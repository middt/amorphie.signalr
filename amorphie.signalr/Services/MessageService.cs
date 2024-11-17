using amorphie.signalr.Database;
using amorphie.signalr.Models;
using amorphie.signalr.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

public class MessageService : IMessageService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<MessageService> _logger;

    public MessageService(
        ApplicationDbContext context,
        IHubContext<NotificationHub> hubContext,
        ILogger<MessageService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task<IEnumerable<Message>> GetUnacknowledgedMessagesAsync(string userId)
    {
        return await _context.Messages
            .Where(m => m.UserId == userId && !m.IsAcknowledged)
            .ToListAsync();
    }

    public async Task<bool> AcknowledgeMessageAsync(string messageId)
    {
        _logger.LogInformation("Attempting to acknowledge message {MessageId}", messageId);

        var message = await _context.Messages.FindAsync(messageId);
        if (message == null)
        {
            _logger.LogWarning("Message {MessageId} not found", messageId);
            return false;
        }

        if (message.IsAcknowledged)
        {
            _logger.LogInformation("Message {MessageId} was already acknowledged", messageId);
            return false;
        }

        message.IsAcknowledged = true;
        message.AcknowledgedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        _logger.LogInformation("Message {MessageId} acknowledged successfully", messageId);
        return true;
    }

    public async Task<Message> SendMessageAsync(string userId, string content)
    {
        _logger.LogInformation("Attempting to send message to user {UserId}", userId);

        var message = new Message
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Content = content,
            IsAcknowledged = false,
            Timestamp = DateTime.UtcNow,
            RetryAttempts = 0,
            MaxRetryAttempts = 3,
            MessageTimeout = TimeSpan.FromHours(24)
        };

        try
        {
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Message {MessageId} saved successfully", message.Id);

            if (NotificationHub.IsUserConnected(userId))
            {
                await _hubContext.Clients.User(userId)
                    .SendAsync("ReceiveMessage", message.Id, content);
                _logger.LogInformation("Message {MessageId} sent to connected user {UserId}", message.Id, userId);
            }
            else
            {
                _logger.LogInformation("User {UserId} not connected. Message {MessageId} will be delivered when user connects", userId, message.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending message {MessageId} to user {UserId}", message.Id, userId);
            throw;
        }

        return message;
    }
}