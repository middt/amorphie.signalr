using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using amorphie.signalr.Database;
using amorphie.signalr.Models;
using Microsoft.EntityFrameworkCore;

public class NotificationHub : Hub
{
    private readonly ApplicationDbContext _context;
    private static ConcurrentDictionary<string, string> _connectedUsers = new ConcurrentDictionary<string, string>();

    public NotificationHub(ApplicationDbContext context)
    {
        _context = context;
    }
    public static bool IsUserConnected(string userId) =>
    !string.IsNullOrEmpty(userId) && _connectedUsers.Any(x => x.Value == userId);

    public override async Task OnConnectedAsync()
    {
        var userId = Context.GetHttpContext()?.Request.Headers["X-User-Id"].FirstOrDefault();
        if (!string.IsNullOrEmpty(userId))
        {
            _connectedUsers[Context.ConnectionId] = userId;
            await ResendUnacknowledgedMessages(userId);
        }
        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception exception)
    {
        _connectedUsers.TryRemove(Context.ConnectionId, out _);
        return base.OnDisconnectedAsync(exception);
    }

    private async Task ResendUnacknowledgedMessages(string userId)
    {
        var unacknowledgedMessages = await _context.Messages
            .Where(m => m.UserId == userId && !m.IsAcknowledged)
            .ToListAsync();

        foreach (var message in unacknowledgedMessages)
        {
            await Clients.User(userId).SendAsync("ReceiveMessage", message.Id, message.Content);
        }
    }

    public async Task SendMessage(string userId, string messageContent)
    {
        var message = new Message
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Content = messageContent,
            IsAcknowledged = false,
            Timestamp = DateTime.UtcNow
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        if (IsUserConnected(userId))
        {
            await Clients.User(userId).SendAsync("ReceiveMessage", message.Id, messageContent);
        }
    }

    public async Task AcknowledgeMessage(string messageId)
    {
        var message = await _context.Messages.FindAsync(messageId);
        if (message != null && !message.IsAcknowledged)
        {
            message.IsAcknowledged = true;
            await _context.SaveChangesAsync();
        }
    }
}
