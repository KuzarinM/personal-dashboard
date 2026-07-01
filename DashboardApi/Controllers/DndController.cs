using System.Text.Json;
using DashboardApi.Data;
using DashboardApi.Data.Models;
using DashboardApi.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DashboardApi.Controllers
{
    [Route("api/dnd")]
    [ApiController]
    [Authorize] // По умолчанию все методы требуют JWT
    public class DndController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<NotificationHub> _hub;

        public DndController(AppDbContext db, IHubContext<NotificationHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        // Безопасное извлечение ID (возвращает 0, если запрос анонимный)
        private int GetUserId()
        {
            var claim = User.FindFirst("id");
            return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
        }

        private async Task<bool> CanEdit(int dashboardId)
        {
            var uid = GetUserId();
            if (uid == 0) return false; // Гости не могут редактировать

            var dash = await _db.Dashboards.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dashboardId);
            if (dash == null) return false;
            if (dash.UserId == uid) return true; // Владелец

            var access = await _db.DashboardAccesses.AsNoTracking()
                .FirstOrDefaultAsync(a => a.DashboardId == dashboardId && a.UserId == uid);
            return access != null && access.Role == "Editor";
        }

        // ==========================================
        // 1. ЛИСТ ПЕРСОНАЖА (Чтение разрешено анонимам, если дашборд IsPublic)
        // ==========================================
        [HttpGet("{dashboardId}/character")]
        [AllowAnonymous] // <-- Разрешаем анонимный доступ для публичных страниц
        public async Task<IActionResult> GetCharacter(int dashboardId)
        {
            var dash = await _db.Dashboards.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dashboardId);
            if (dash == null) return NotFound("Dashboard not found");

            var userId = GetUserId();
            bool hasAccess = false;

            if (dash.IsPublic)
            {
                hasAccess = true; // Гости могут просматривать публичные страницы
            }
            else if (userId != 0)
            {
                if (dash.UserId == userId)
                {
                    hasAccess = true;
                }
                else
                {
                    hasAccess = await _db.DashboardAccesses.AnyAsync(a => a.DashboardId == dashboardId && a.UserId == userId);
                }
            }

            if (!hasAccess) return Forbid();

            var character = await _db.DndCharacters.AsNoTracking().FirstOrDefaultAsync(c => c.DashboardId == dashboardId);
            if (character == null)
            {
                return Ok(new { });
            }

            return Content(character.DataJson, "application/json");
        }

        [HttpPut("{dashboardId}/character")]
        [AllowAnonymous] // <-- Разрешаем вызов анонимным гостям
        public async Task<IActionResult> SaveCharacter(int dashboardId, [FromBody] JsonElement data)
        {
            var dash = await _db.Dashboards.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dashboardId);
            if (dash == null) return NotFound("Dashboard not found");

            // ИСПРАВЛЕНИЕ: Если дашборд публичный, разрешаем сохранение ВСЕМ (включая анонимов)
            // Если приватный — строго требуем права CanEdit ( Owner или Editor )
            if (!dash.IsPublic)
            {
                if (!await CanEdit(dashboardId)) return Forbid();
            }

            var character = await _db.DndCharacters.FirstOrDefaultAsync(c => c.DashboardId == dashboardId);
            var jsonStr = data.ToString();

            if (character == null)
            {
                character = new DndCharacter
                {
                    DashboardId = dashboardId,
                    DataJson = jsonStr
                };
                _db.DndCharacters.Add(character);
            }
            else
            {
                character.DataJson = jsonStr;
            }

            await _db.SaveChangesAsync();

            // Оповещаем все открытые вкладки этого дашборда по SignalR
            await _hub.Clients.Group(dashboardId.ToString()).SendAsync("InvalidateData", "dnd_character");

            return Ok(new { success = true });
        }

        // ==========================================
        // 2. ОБЩИЙ КАТАЛОГ (Разрешен анонимам, если передан публичный dashboardId)
        // ==========================================

        [HttpGet("catalog")]
        [AllowAnonymous] // <-- Разрешаем анонимный доступ
        public async Task<IActionResult> GetCatalog([FromQuery] int? dashboardId)
        {
            int targetUserId = 0;
            var userId = GetUserId();

            if (dashboardId.HasValue)
            {
                var dash = await _db.Dashboards.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dashboardId.Value);
                if (dash == null) return NotFound("Dashboard not found");

                // Если дашборд приватный - проверяем доступ
                if (!dash.IsPublic && dash.UserId != userId)
                {
                    var hasAccess = await _db.DashboardAccesses.AnyAsync(a => a.DashboardId == dashboardId.Value && a.UserId == userId);
                    if (!hasAccess) return Forbid();
                }

                targetUserId = dash.UserId; // Читаем каталог владельца этого дашборда
            }
            else
            {
                // Запасной вариант для запросов вне дашборда
                if (userId == 0) return Unauthorized("Authentication required");
                targetUserId = userId;
            }

            var catalog = await _db.DndCatalogs.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == targetUserId);
            if (catalog == null)
            {
                return Ok(new { items = new List<object>(), spells = new List<object>() });
            }

            return Content(catalog.DataJson, "application/json");
        }

        [HttpPut("catalog")]
        [AllowAnonymous] // <-- Разрешаем анонимное изменение
        public async Task<IActionResult> SaveCatalog([FromQuery] int? dashboardId, [FromBody] JsonElement data)
        {
            int targetUserId = 0;
            var userId = GetUserId();

            if (dashboardId.HasValue)
            {
                var dash = await _db.Dashboards.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dashboardId.Value);
                if (dash == null) return NotFound("Dashboard not found");

                // Если дашборд публичный — любой гость может пополнять каталог владельца.
                // Если приватный — строго требуем права CanEdit (Владелец/Редактор)
                if (!dash.IsPublic)
                {
                    if (!await CanEdit(dashboardId.Value)) return Forbid();
                }

                targetUserId = dash.UserId;
            }
            else
            {
                if (userId == 0) return Unauthorized("Authentication required");
                targetUserId = userId;
            }

            var catalog = await _db.DndCatalogs.FirstOrDefaultAsync(c => c.UserId == targetUserId);
            var jsonStr = data.ToString();

            if (catalog == null)
            {
                catalog = new DndCatalog
                {
                    UserId = targetUserId,
                    DataJson = jsonStr
                };
                _db.DndCatalogs.Add(catalog);
            }
            else
            {
                catalog.DataJson = jsonStr;
            }

            await _db.SaveChangesAsync();

            // Рассылаем SignalR оповещение всем открытым вкладкам дашборда
            if (dashboardId.HasValue)
            {
                await _hub.Clients.Group(dashboardId.Value.ToString()).SendAsync("InvalidateData", "dnd_catalog");
            }
            else
            {
                await _hub.Clients.User(targetUserId.ToString()).SendAsync("InvalidateData", "dnd_catalog");
            }

            return Ok(new { success = true });
        }
    }
}