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
        var message = await _context.Messages.FindAsync(messageId);
        if (message == null || message.IsAcknowledged)
            return false;

        message.IsAcknowledged = true;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<Message> SendMessageAsync(string userId, string content)
    {
        var message = new Message
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Content = content,
            IsAcknowledged = false,
            Timestamp = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        if (NotificationHub.IsUserConnected(userId))
        {
            await _hubContext.Clients.User(userId)
                .SendAsync("ReceiveMessage", message.Id, content);
        }

        return message;
    }
}