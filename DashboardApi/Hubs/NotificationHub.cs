using Microsoft.AspNetCore.SignalR;
using System.Text.RegularExpressions;

namespace DashboardApi.Hubs
{
    public class NotificationHub : Hub
    {
        // Клиент при подключении говорит "Я слушаю дашборд №5"
        public async Task JoinDashboard(string dashboardId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, dashboardId);
        }

        public async Task SendUpdate(string dashboardId, string target)
        {
            await Clients.Group(dashboardId).SendAsync("InvalidateData", target);
        }
    }
}
