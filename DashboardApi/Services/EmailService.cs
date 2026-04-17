using DashboardApi.Data;
using DashboardApi.DTOs.Email;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace DashboardApi.Services
{
    public class EmailService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public EmailService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task<object> GetUnreadEmailsAsync(int dashboardId)
        {
            EmailConfigDto? config = null;

            // 1. Получаем конфиг
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var integration = await db.Integrations.AsNoTracking()
                    .FirstOrDefaultAsync(i => i.DashboardId == dashboardId && i.Type == "Email");

                if (integration == null || string.IsNullOrEmpty(integration.ConfigJson))
                    return new { notConfigured = true };

                try { config = JsonConvert.DeserializeObject<EmailConfigDto>(integration.ConfigJson); }
                catch { return new { notConfigured = true }; }
            }

            if (config == null || string.IsNullOrEmpty(config.Host)) return new { notConfigured = true };

            // 2. Подключаемся через MailKit
            using var client = new  ImapClient();
            try
            {
                // Таймаут поменьше, чтобы не висело
                client.Timeout = 10000;

                await client.ConnectAsync(config.Host, config.Port, config.UseSsl);
                await client.AuthenticateAsync(config.Username, config.Password);

                var inbox = client.Inbox;
                await inbox.OpenAsync(FolderAccess.ReadOnly);

                // 3. Ищем НЕПРОЧИТАННЫЕ
                var uids = await inbox.SearchAsync(SearchQuery.NotSeen);

                // Берем последние 10 (самые свежие, т.к. UIDs растут)
                var recentUids = uids.OrderByDescending(u => u).Take(10).ToList();

                var result = new List<EmailMessageDto>();

                // Запрашиваем только заголовки (Summary), не качаем тела писем!
                foreach (var summary in await inbox.FetchAsync(recentUids, MessageSummaryItems.Envelope | MessageSummaryItems.InternalDate))
                {
                    result.Add(new EmailMessageDto(
                        Id: summary.UniqueId.ToString(),
                        From: summary.Envelope.From.Count > 0 ? summary.Envelope.From[0].Name ?? summary.Envelope.From[0].ToString() : "Unknown",
                        Subject: summary.Envelope.Subject ?? "(No Subject)",
                        Date: summary.InternalDate?.UtcDateTime ?? DateTime.UtcNow,
                        IsNew: true
                    ));
                }

                await client.DisconnectAsync(true);

                // Сортируем: свежие сверху
                return result.OrderByDescending(x => x.Date).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Email] Error: {ex.Message}");
                return new { error = ex.Message };
            }
        }
    }
}
