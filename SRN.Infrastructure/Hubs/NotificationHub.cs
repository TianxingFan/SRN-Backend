using Microsoft.AspNetCore.SignalR;

namespace SRN.Infrastructure.Hubs
{
    /// <summary>
    /// SignalR WebSocket Hub responsible for pushing real-time events to connected clients.
    /// Acts as the central nervous system for async UI updates.
    /// </summary>
    public class NotificationHub : Hub
    {
        /// <summary>
        /// Intercepts the connection event when a client establishes a WebSocket connection.
        /// Maps the physical connection to a logical user grouping.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var httpContext = Context.GetHttpContext();

            // Extract the user ID passed as a query string parameter during the handshake
            var userId = httpContext?.Request.Query["user"].ToString();

            if (!string.IsNullOrEmpty(userId))
            {
                // Add the specific connection to a SignalR Group named after their User ID.
                // This enables the server to send private, targeted notifications to a specific user
                // even if they have multiple browser tabs open simultaneously.
                await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            }

            await base.OnConnectedAsync();
        }

        /// <summary>
        /// Handles cleanup when a client drops the WebSocket connection.
        /// </summary>
        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            await base.OnDisconnectedAsync(exception);
        }
    }
}