using Microsoft.AspNetCore.SignalR;
using SRN.Application.Interfaces;
using SRN.Infrastructure.Hubs;

namespace SRN.Infrastructure.Services
{
    /// <summary>
    /// Wrapper service around the SignalR Hub Context.
    /// Allows other parts of the application (like background threads) to push real-time WebSocket 
    /// notifications to connected frontend clients without needing a direct HTTP request/response cycle.
    /// </summary>
    public class SignalRNotificationService : INotificationService
    {
        private readonly IHubContext<NotificationHub> _hubContext;

        public SignalRNotificationService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
        }

        /// <summary>
        /// Dispatches a success message to a specific user using their isolated SignalR group.
        /// </summary>
        public async Task SendSuccessAsync(string userId, string message, string artifactId)
        {
            // The frontend listens for the "ReceiveMessage" event signature
            await _hubContext.Clients.Group(userId).SendAsync("ReceiveMessage", "System", message, artifactId);
        }

        /// <summary>
        /// Dispatches a failure/error message to a specific user using their isolated SignalR group.
        /// </summary>
        public async Task SendFailureAsync(string userId, string message, string artifactId)
        {
            await _hubContext.Clients.Group(userId).SendAsync("ReceiveMessage", "System", message, artifactId);
        }
    }
}