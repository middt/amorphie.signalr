using Dapr.Actors;
using amorphie.signalr.Models;

namespace amorphie.signalr.Actors;

public interface IMessageActor : IActor
{
    Task<Message> SendMessageAsync(string userId, string content);
    Task<bool> AcknowledgeMessageAsync(string messageId);
    Task<bool> RetryMessageAsync(string messageId);
    Task<IEnumerable<Message>> GetUnacknowledgedMessagesAsync(string userId);
}