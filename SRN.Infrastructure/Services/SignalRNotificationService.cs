using Microsoft.AspNetCore.SignalR;
using SRN.Application.Interfaces;
using SRN.Infrastructure.Hubs;

namespace SRN.Infrastructure.Services
{
    public class SignalRNotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public SignalRNotificationService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task SendSuccessAsync(string userId, string message, string artifactId)
        {
            await _hubContext.Clients.Group(userId).SendAsync("ReceiveMessage", "System", message, artifactId);
        }

        public async Task SendFailureAsync(string userId, string message, string artifactId)
        {
            await _hubContext.Clients.Group(userId).SendAsync("ReceiveMessage", "System", message, artifactId);
        }
    }
}