namespace amorphie.signalr.Models;

public class Message
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string Content { get; set; }
    public MessageState State { get; set; }
    public DateTime Timestamp { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public int RetryAttempts { get; set; }
    public int MaxRetryAttempts { get; set; } = 3;
    public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromHours(24);

    public bool IsAcknowledged => State == MessageState.Acknowledged;
    public bool IsExpired => State == MessageState.Expired ||
        (!IsAcknowledged && DateTime.UtcNow > Timestamp.Add(MessageTimeout));
}