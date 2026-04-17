using DashboardApi.Data;
using DashboardApi.Data.Models;
using DashboardApi.Hubs; // <--- Важно
using Microsoft.AspNetCore.SignalR; // <--- Важно
using Microsoft.EntityFrameworkCore;
using System.Net.NetworkInformation;

namespace DashboardApi.Services
{
    public class MonitoringBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IHubContext<NotificationHub> _hub; // <--- Добавили Хаб

        public MonitoringBackgroundService(
            IServiceProvider services,
            IHttpClientFactory httpFactory,
            IHubContext<NotificationHub> hub) // <--- Инжектируем
        {
            _services = services;
            _httpFactory = httpFactory;
            _hub = hub;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Даем приложению немного времени на старт
            await Task.Delay(5000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _services.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var now = DateTime.UtcNow;

                        // Ищем активные задачи, время которых пришло
                        var tasks = await db.Monitors
                            .Where(m => m.IsActive)
                            .ToListAsync(stoppingToken);

                        var dueTasks = tasks
                            .Where(m => m.LastCheck == null || m.LastCheck.Value.AddMinutes(m.IntervalMin) <= now)
                            .ToList();

                        // Коллекция ID дашбордов, которые нужно уведомить
                        var dashboardsToNotify = new HashSet<int>();

                        foreach (var item in dueTasks)
                        {
                            await CheckTarget(item);

                            // Добавляем ID дашборда в список на уведомление
                            dashboardsToNotify.Add(item.DashboardId);
                        }

                        if (dueTasks.Any())
                        {
                            await db.SaveChangesAsync(stoppingToken);

                            // Рассылаем уведомления (только тем дашбордам, чьи мониторы мы проверили)
                            foreach (var dashId in dashboardsToNotify)
                            {
                                await _hub.Clients.Group(dashId.ToString())
                                    .SendAsync("InvalidateData", "monitoring", stoppingToken);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Monitor Service Error] {ex.Message}");
                }

                // Проверяем каждую минуту
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }

        private async Task CheckTarget(MonitorItem item)
        {
            item.LastCheck = DateTime.UtcNow;
            try
            {
                if (item.Type == "Ping")
                {
                    using var ping = new Ping();
                    // Чистим хост от протоколов и путей
                    var host = item.Target
                        .Replace("http://", "")
                        .Replace("https://", "")
                        .Split('/')[0];

                    var reply = await ping.SendPingAsync(host, 2000); // 2 sec timeout

                    item.IsUp = reply.Status == IPStatus.Success;
                    item.ResponseTimeMs = reply.RoundtripTime;
                    item.LastError = item.IsUp ? null : reply.Status.ToString();
                }
                else if (item.Type == "Http")
                {
                    using var client = _httpFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(10);

                    var url = item.Target.StartsWith("http") ? item.Target : "https://" + item.Target;

                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    // Используем GetAsync, чтобы проверить реальный ответ, а не просто коннект
                    var response = await client.GetAsync(url);
                    sw.Stop();

                    item.IsUp = response.IsSuccessStatusCode;
                    item.ResponseTimeMs = sw.ElapsedMilliseconds;
                    item.LastError = item.IsUp ? null : $"{response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                item.IsUp = false;
                item.ResponseTimeMs = 0;
                item.LastError = ex.Message;
            }
        }
    }
}