namespace amorphie.signalr.Models;

public class Message
{
    public string Id { get; set; }
    public string UserId { get; set; }
    public string Content { get; set; }
    public bool IsAcknowledged { get; set; }
    public DateTime Timestamp { get; set; }
}