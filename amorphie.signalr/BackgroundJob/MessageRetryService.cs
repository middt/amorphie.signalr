using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.SignalR;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using amorphie.signalr.Database;
using Microsoft.EntityFrameworkCore;

public class MessageRetryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _retryInterval = TimeSpan.FromMinutes(1);

    public MessageRetryService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();

                var unacknowledgedMessages = await context.Messages
                    .Where(m => !m.IsAcknowledged)
                    .ToListAsync();

                foreach (var message in unacknowledgedMessages)
                {
                    var userId = message.UserId;
                    if (NotificationHub.IsUserConnected(userId))
                    {
                        await hubContext.Clients.User(userId).SendAsync("ReceiveMessage", message.Id, message.Content);
                    }
                }
            }

            await Task.Delay(_retryInterval, stoppingToken);
        }
    }
}