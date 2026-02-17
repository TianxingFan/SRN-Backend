using Microsoft.AspNetCore.SignalR;

namespace SRN.API.Hubs
{
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            // 1. 获取前端传来的 userId (例如: /notificationHub?user=111-222)
            var httpContext = Context.GetHttpContext();
            var userId = httpContext?.Request.Query["user"].ToString();

            // 2. 如果 userId 存在，把这个连接加入到以 userId 命名的“组”里
            if (!string.IsNullOrEmpty(userId))
            {
                // Groups.AddToGroupAsync(连接ID, 组名/用户ID)
                await Groups.AddToGroupAsync(Context.ConnectionId, userId);
            }

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            // SignalR 会自动处理退组，但你也可以手动记录日志
            await base.OnDisconnectedAsync(exception);
        }
    }
}