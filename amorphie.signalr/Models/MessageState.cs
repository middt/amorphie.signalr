namespace amorphie.signalr.Models;

public enum MessageState
{
    Created,
    Queued,
    Delivered,
    Acknowledged,
    Failed,
    Expired
}