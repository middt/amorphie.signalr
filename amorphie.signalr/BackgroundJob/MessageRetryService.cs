using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using amorphie.signalr.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class MessageRetryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MessageRetryService> _logger;
    private readonly TimeSpan _retryInterval = TimeSpan.FromMinutes(1);

    public MessageRetryService(
        IServiceScopeFactory scopeFactory,
        ILogger<MessageRetryService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessUnacknowledgedMessages(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing unacknowledged messages");
            }

            await Task.Delay(_retryInterval, stoppingToken);
        }
    }

    private async Task ProcessUnacknowledgedMessages(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();

        var unacknowledgedMessages = await context.Messages
            .Where(m => !m.IsAcknowledged
                && m.RetryAttempts < m.MaxRetryAttempts
                && !m.IsExpired)
            .ToListAsync(stoppingToken);

        foreach (var message in unacknowledgedMessages)
        {
            if (NotificationHub.IsUserConnected(message.UserId))
            {
                try
                {
                    await hubContext.Clients.User(message.UserId)
                        .SendAsync("ReceiveMessage", message.Id, message.Content, cancellationToken: stoppingToken);

                    message.RetryAttempts++;
                    _logger.LogInformation(
                        "Retry attempt {RetryAttempt}/{MaxRetries} for message {MessageId} to user {UserId}",
                        message.RetryAttempts, message.MaxRetryAttempts, message.Id, message.UserId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error retrying message {MessageId} to user {UserId}",
                        message.Id, message.UserId);
                }
            }
        }

        await context.SaveChangesAsync(stoppingToken);

        // Handle expired messages
        var expiredMessages = await context.Messages
            .Where(m => !m.IsAcknowledged && m.IsExpired)
            .ToListAsync(stoppingToken);

        if (expiredMessages.Any())
        {
            _logger.LogWarning("Found {Count} expired messages", expiredMessages.Count);
            // You might want to move expired messages to an archive table or handle them differently
        }
    }
}