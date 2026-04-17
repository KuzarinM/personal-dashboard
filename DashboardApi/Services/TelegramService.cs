using DashboardApi.Data;
using DashboardApi.Data.Models;
using Jering.Javascript.NodeJS;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace DashboardApi.Services
{

    public class NodeAuthResponse
    {
        public string status { get; set; }
        public string session { get; set; }
        public string error { get; set; }
    }
    public class TelegramService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly INodeJSService _nodeService;
        private readonly string _scriptPath;

        public TelegramService(IServiceScopeFactory scopeFactory, INodeJSService nodeService)
        {
            _scopeFactory = scopeFactory;
            _nodeService = nodeService;

            _scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "NodeScripts", "telegram.js");

            if (!File.Exists(_scriptPath))
            {
                // Фолбэк для локальной разработки, если нужно
                _scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "NodeScripts", "telegram.js");
            }
        }

        // --- ВЫЗОВ JS ---
        private async Task<T?> InvokeJsAsync<T>(string action, int dashboardId, Dictionary<string, object>? payload = null)
        {
            // Формируем аргументы через Dictionary, чтобы контролировать имена полей (lowercase)
            var argsMap = new Dictionary<string, object>
            {
                { "action", action },
                { "dashboardId", dashboardId },
                { "payload", payload ?? new Dictionary<string, object>() }
            };

            // Лог для отладки
            // Console.WriteLine($"[C#->JS] {action} for {dashboardId}");

            return await _nodeService.InvokeFromFileAsync<T>(_scriptPath, args: new object[] { argsMap });
        }

        // --- API ---

        public async Task<object> GetUnreadMessagesAsync(int dashboardId)
        {
            // Достаем строку сессии из БД
            string session = "";
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var integration = await db.Integrations.AsNoTracking()
                    .FirstOrDefaultAsync(i => i.DashboardId == dashboardId && i.Type == "Telegram");

                if (integration != null && !string.IsNullOrEmpty(integration.ConfigJson))
                {
                    dynamic conf = JsonConvert.DeserializeObject(integration.ConfigJson);
                    session = conf?.session;
                }
            }

            if (string.IsNullOrEmpty(session)) return new { notConfigured = true };

            try
            {
                // Вызываем JS
                var result = await InvokeJsAsync<object>("getMessages", dashboardId, new(){ { "session", session} });
                return result ?? new List<object>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NodeJS] Error: {ex.Message}");
                // Проверяем, не вылетел ли JS с ошибкой авторизации
                if (ex.Message.Contains("AUTH_KEY_UNREGISTERED") || ex.Message.Contains("401"))
                {
                    return new { notConfigured = true };
                }
                return new { error = "Internal error" };
            }
        }

        public async Task<string> StartAuthAsync(int dashboardId, string phone)
        {
            var payload = new Dictionary<string, object>
            {
                { "phone", phone }
            };

            // ИСПОЛЬЗУЕМ ТИПИЗИРОВАННЫЙ ОТВЕТ
            var res = await InvokeJsAsync<NodeAuthResponse>("auth_start", dashboardId, payload);

            // Теперь C# точно знает, что такое .status
            return res?.status ?? "ERROR";
        }

        public async Task<string> CompleteAuthAsync(int dashboardId, string code, string? password = null)
        {
            var payload = new Dictionary<string, object>
            {
                { "code", code },
                { "password", password }
            };

            var res = await InvokeJsAsync<NodeAuthResponse>("auth_complete", dashboardId, payload);

            if (res?.status == "SUCCESS")
            {
                // Сохраняем сессию
                string session = res.session;
                await SaveSession(dashboardId, session);
                return "SUCCESS";
            }

            // Возвращаем статус ошибки (например PASSWORD_NEEDED) или ERROR
            return res?.status ?? "ERROR";
        }
        private async Task SaveSession(int dashboardId, string session)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var item = await db.Integrations.FirstOrDefaultAsync(i => i.DashboardId == dashboardId && i.Type == "Telegram");
            if (item == null)
            {
                item = new Integration { DashboardId = dashboardId, Type = "Telegram" };
                db.Integrations.Add(item);
            }

            item.ConfigJson = JsonConvert.SerializeObject(new { session, active = true });
            item.SessionData = null; // Не используем блоб
            await db.SaveChangesAsync();
        }
    }
}