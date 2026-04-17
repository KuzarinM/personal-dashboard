using DashboardApi.Data;
using DashboardApi.Data.Models;
using DashboardApi.DTOs.Dashboard;
using DashboardApi.DTOs.Users;
using DashboardApi.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace DashboardApi.Controllers
{
    [Route("api/users/status")]
    [ApiController]
    [Authorize]
    public class UserStatusController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<NotificationHub> _hub;

        public UserStatusController(AppDbContext db, IHubContext<NotificationHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        private int GetUserId() => int.Parse(User.FindFirst("id")?.Value ?? "0");

        // DTO для настроек виджета (список наблюдения)
        public class UserStatusConfigDto
        {
            public List<string> ExtraUsers { get; set; } = new();
        }

        // 1. Получить мой статус
        [HttpGet("me")]
        public async Task<IActionResult> GetMyStatus()
        {
            var user = await _db.Users.FindAsync(GetUserId());
            if (user == null) return Unauthorized();

            return Ok(new UserStatusDto(
                user.Username,
                user.StatusText ?? "Online",
                user.StatusEmoji ?? "🟢",
                user.StatusMessage ?? "",
                user.StatusColor ?? "emerald",
                user.LastStatusUpdate
            ));
        }

        // 2. Обновить мой статус
        [HttpPut("me")]
        public async Task<IActionResult> SetMyStatus([FromBody] UpdateUserStatusDto dto)
        {
            var userId = GetUserId();
            var user = await _db.Users
                .Include(u => u.Dashboards) // Загружаем дашборды, где я владелец
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return Unauthorized();

            user.StatusText = dto.StatusText;
            user.StatusEmoji = dto.StatusEmoji;
            user.StatusMessage = dto.StatusMessage;
            user.StatusColor = dto.StatusColor;
            user.LastStatusUpdate = DateTime.UtcNow; // Всегда храним в UTC

            await _db.SaveChangesAsync();

            // --- УВЕДОМЛЕНИЯ (SignalR) ---
            // Нам нужно уведомить все дашборды, на которых присутствует этот пользователь,
            // чтобы виджеты у коллег обновились мгновенно.

            // 1. Дашборды, где я Владелец
            var myDashIds = user.Dashboards.Select(d => d.Id).ToList();

            // 2. Дашборды, где я Гость (Editor/Viewer)
            var guestDashIds = await _db.DashboardAccesses
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .Select(a => a.DashboardId)
                .ToListAsync();

            // 3. Объединяем и шлем сигнал
            var allDashIds = myDashIds.Concat(guestDashIds).Distinct();

            foreach (var dashId in allDashIds)
            {
                await _hub.Clients.Group(dashId.ToString()).SendAsync("InvalidateData", "userstatus");
            }

            return Ok(new { success = true });
        }

        // 3. Получить статусы списка пользователей (Batch request)
        [HttpPost("batch")]
        public async Task<IActionResult> GetBatchStatuses([FromBody] List<string> usernames)
        {
            if (usernames == null || !usernames.Any()) return Ok(new List<UserStatusDto>());

            // Нормализуем для поиска
            var uniqueNames = usernames.Distinct().Select(u => u.ToLower()).ToList();

            var users = await _db.Users
                .AsNoTracking()
                .Where(u => uniqueNames.Contains(u.Username.ToLower()))
                .ToListAsync();

            var result = users.Select(u => new UserStatusDto(
                u.Username,
                u.StatusText ?? "Unknown",
                u.StatusEmoji ?? "⚫",
                u.StatusMessage ?? "",
                u.StatusColor ?? "zinc",
                u.LastStatusUpdate
            )).ToList();

            return Ok(result);
        }

        // 4. Получить настройки виджета (Watchlist) для конкретного дашборда
        [HttpGet("settings/{dashboardId}")]
        public async Task<IActionResult> GetWidgetSettings(int dashboardId)
        {
            // Здесь можно добавить проверку доступа (читаем настройки, если есть доступ к дашборду)
            var uid = GetUserId();
            var dash = await _db.Dashboards.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dashboardId);
            if (dash == null) return NotFound();

            if (!dash.IsPublic && dash.UserId != uid)
            {
                var hasAccess = await _db.DashboardAccesses.AnyAsync(a => a.DashboardId == dashboardId && a.UserId == uid);
                if (!hasAccess) return Forbid();
            }

            var integration = await _db.Integrations
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.DashboardId == dashboardId && i.Type == "UserStatus");

            if (integration == null || string.IsNullOrEmpty(integration.ConfigJson))
            {
                return Ok(new UserStatusConfigDto());
            }

            // Возвращаем JSON как есть
            return Content(integration.ConfigJson, "application/json");
        }

        // 5. Сохранить настройки виджета (Watchlist)
        [HttpPost("settings/{dashboardId}")]
        public async Task<IActionResult> SaveWidgetSettings(int dashboardId, [FromBody] UserStatusConfigDto dto)
        {
            var uid = GetUserId();
            var dash = await _db.Dashboards.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dashboardId);

            if (dash == null) return NotFound();

            // Проверка прав: Только Владелец или Редактор
            if (dash.UserId != uid)
            {
                var access = await _db.DashboardAccesses.AsNoTracking()
                    .FirstOrDefaultAsync(a => a.DashboardId == dashboardId && a.UserId == uid);

                if (access == null || access.Role != "Editor") return Forbid();
            }

            var integration = await _db.Integrations.FirstOrDefaultAsync(i => i.DashboardId == dashboardId && i.Type == "UserStatus");
            if (integration == null)
            {
                integration = new Integration
                {
                    DashboardId = dashboardId,
                    Type = "UserStatus"
                };
                _db.Integrations.Add(integration);
            }

            // Сериализуем DTO (теперь это корректный объект)
            integration.ConfigJson = JsonConvert.SerializeObject(dto);

            await _db.SaveChangesAsync();

            // Уведомляем фронтенд этого дашборда, чтобы обновить список
            await _hub.Clients.Group(dashboardId.ToString()).SendAsync("InvalidateData", "userstatus");

            return Ok(new { success = true });
        }
    }
}