using DashboardApi.Data;
using DashboardApi.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DashboardApi.Services
{
    public class ReminderBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly IHubContext<NotificationHub> _hub;

        public ReminderBackgroundService(IServiceProvider services, IHubContext<NotificationHub> hub)
        {
            _services = services;
            _hub = hub;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _services.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                        var now = DateTime.UtcNow;

                        // Ищем напоминания, которые наступили "только что" (в пределах последней минуты)
                        // Чтобы не спамить, можно добавить флаг в БД, но пока делаем по времени
                        var reminders = await db.Reminders
                            .AsNoTracking() // Важно для перформанса
                            .Where(r => r.TargetTime <= now && r.TargetTime > now.AddSeconds(-30))
                            .ToListAsync(stoppingToken);

                        foreach (var r in reminders)
                        {
                            // Пушим уведомление в группу конкретного дашборда
                            await _hub.Clients.Group(r.DashboardId.ToString())
                                .SendAsync("ReceiveReminder", new
                                {
                                    id = r.Id,
                                    message = r.Message,
                                    isRecurring = r.RecurrenceType != "None"
                                }, stoppingToken);
                        }

                        // Обработка повторяющихся задач (Recurrence) - это лучше делать отдельным воркером,
                        // который меняет TargetTime в базе. Для краткости пока опустим, 
                        // фронт и так умеет делать "Acknowledge", который двигает время.
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[BG Service Error] {ex.Message}");
                }

                // Ждем 10 секунд перед следующей проверкой
                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}
