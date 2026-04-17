using DashboardApi.Data;
using DashboardApi.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Collections.Concurrent;

namespace DashboardApi.Services
{
    public class SmartPollerService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly TelegramService _tgService;
        private readonly IHubContext<NotificationHub> _hub;
        private readonly ILogger<SmartPollerService> _logger; // <--- Логгер

        private readonly ConcurrentDictionary<string, string> _stateCache = new();

        public SmartPollerService(
            IServiceProvider services,
            TelegramService tgService,
            IHubContext<NotificationHub> hub,
            ILogger<SmartPollerService> logger)
        {
            _services = services;
            _tgService = tgService;
            _hub = hub;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(5000, stoppingToken); // Ждем старта

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckIntegrations(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[SmartPoller] Global Loop Error");
                }

                await Task.Delay(30000, stoppingToken); // 30 сек
            }
        }

        private async Task CheckIntegrations(CancellationToken ct)
        {
            using var scope = _services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var integrations = await db.Integrations
                .AsNoTracking()
                .Where(i => i.Type == "Telegram" || i.Type == "Email")
                .ToListAsync(ct);

            _logger.LogInformation($"[SmartPoller] Checking {integrations.Count} integrations...");

            var tasks = integrations.Select(async item =>
            {
                if (item.Type == "Telegram") await CheckTelegram(item);
                else if (item.Type == "Email") await CheckEmail(item);
            });

            await Task.WhenAll(tasks);
        }

        private async Task CheckTelegram(Data.Models.Integration item)
        {
            try
            {
                // Получаем объект (List<object>)
                var result = await _tgService.GetUnreadMessagesAsync(item.DashboardId);

                // Проверка на ошибки/пустоту
                var jsonResult = JsonSerializer.Serialize(result);
                if (jsonResult.Contains("notConfigured") || jsonResult.Contains("error")) return;

                var key = $"{item.DashboardId}_telegram";

                // Логика обнаружения изменений
                bool hasChanged = false;
                if (!_stateCache.TryGetValue(key, out var oldHash))
                {
                    // Первый запуск - просто сохраняем, не спамим (или спамим, чтобы инициализировать)
                    _stateCache[key] = jsonResult;
                }
                else if (oldHash != jsonResult)
                {
                    hasChanged = true;
                    _stateCache[key] = jsonResult;
                }

                if (hasChanged)
                {
                    _logger.LogWarning($"[SmartPoller] CHANGE DETECTED for Dash {item.DashboardId} (Telegram). Sending Signal...");

                    await _hub.Clients.Group(item.DashboardId.ToString())
                        .SendAsync("InvalidateData", "telegram");
                }
                else
                {
                    // Раскомментируй для глубокой отладки, если тишина
                    _logger.LogInformation($"[SmartPoller] No changes for Dash {item.DashboardId}. Hash length: {jsonResult.Length}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[SmartPoller] Error checking Telegram for Dash {item.DashboardId}");
            }
        }

        private async Task CheckEmail(Data.Models.Integration item)
        {
            try
            {
                using var scope = _services.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

                var result = await emailService.GetUnreadEmailsAsync(item.DashboardId);
                var jsonResult = JsonSerializer.Serialize(result);
                if (jsonResult.Contains("notConfigured") || jsonResult.Contains("error")) return;

                var key = $"{item.DashboardId}_email";

                if (!_stateCache.TryGetValue(key, out var oldHash) || oldHash != jsonResult)
                {
                    _stateCache[key] = jsonResult;

                    _logger.LogWarning($"[SmartPoller] CHANGE DETECTED for Dash {item.DashboardId} (Email). Sending Signal...");

                    await _hub.Clients.Group(item.DashboardId.ToString())
                        .SendAsync("InvalidateData", "email");
                }
            }
            catch { }
        }
    }
}