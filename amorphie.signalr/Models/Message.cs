namespace amorphie.signalr.Models;

public class Message
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string Content { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTime Timestamp { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public int RetryAttempts { get; set; }
    public int MaxRetryAttempts { get; set; } = 3; // Default value
    public TimeSpan MessageTimeout { get; set; } = TimeSpan.FromHours(24); // Default timeout
    public bool IsExpired => !IsAcknowledged && DateTime.UtcNow > Timestamp.Add(MessageTimeout);
}