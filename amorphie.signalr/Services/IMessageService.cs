using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using amorphie.signalr.Models;

namespace amorphie.signalr.Services
{
    public interface IMessageService
    {
        Task<IEnumerable<Message>> GetUnacknowledgedMessagesAsync(string userId);
        Task<Message> SendMessageAsync(string userId, string content);
        Task<bool> AcknowledgeMessageAsync(string messageId);
    }
}