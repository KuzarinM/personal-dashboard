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
    [Authorize]
    public class DndController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<NotificationHub> _hub;

        public DndController(AppDbContext db, IHubContext<NotificationHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        private int GetUserId() => int.Parse(User.FindFirst("id")?.Value ?? "0");

        // Проверка прав на редактирование дашборда (Владелец или Редактор)
        private async Task<bool> CanEdit(int dashboardId)
        {
            var uid = GetUserId();
            var dash = await _db.Dashboards.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dashboardId);
            if (dash == null) return false;
            if (dash.UserId == uid) return true;

            var access = await _db.DashboardAccesses.AsNoTracking()
                .FirstOrDefaultAsync(a => a.DashboardId == dashboardId && a.UserId == uid);
            return access != null && access.Role == "Editor";
        }

        // ==========================================
        // 1. ЛИСТ ПЕРСОНАЖА (DndCharacter)
        // ==========================================

        [HttpGet("{dashboardId}/character")]
        public async Task<IActionResult> GetCharacter(int dashboardId)
        {
            var dash = await _db.Dashboards.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dashboardId);
            if (dash == null) return NotFound("Dashboard not found");

            // Проверка прав на чтение (если приватный)
            if (!dash.IsPublic && dash.UserId != GetUserId())
            {
                var hasAccess = await _db.DashboardAccesses.AnyAsync(a => a.DashboardId == dashboardId && a.UserId == GetUserId());
                if (!hasAccess) return Forbid();
            }

            var character = await _db.DndCharacters.FirstOrDefaultAsync(c => c.DashboardId == dashboardId);
            if (character == null)
            {
                // Возвращаем пустую структуру, фронтенд заполнит её дефолтными моками
                return Ok(new { });
            }

            return Content(character.DataJson, "application/json");
        }

        [HttpPut("{dashboardId}/character")]
        public async Task<IActionResult> SaveCharacter(int dashboardId, [FromBody] JsonElement data)
        {
            if (!await CanEdit(dashboardId)) return Forbid();

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

            // Сигнал SignalR для мгновенной инвалидации данных на фронтенде
            await _hub.Clients.Group(dashboardId.ToString()).SendAsync("InvalidateData", "dnd_character");

            return Ok(new { success = true });
        }

        // ==========================================
        // 2. ОБЩИЙ КАТАЛОГ (DndCatalog)
        // ==========================================

        [HttpGet("catalog")]
        public async Task<IActionResult> GetCatalog()
        {
            var userId = GetUserId();
            var catalog = await _db.DndCatalogs.FirstOrDefaultAsync(c => c.UserId == userId);
            if (catalog == null)
            {
                // Возвращаем пустую структуру
                return Ok(new { items = new List<object>(), spells = new List<object>() });
            }

            return Content(catalog.DataJson, "application/json");
        }

        [HttpPut("catalog")]
        public async Task<IActionResult> SaveCatalog([FromBody] JsonElement data)
        {
            var userId = GetUserId();
            var catalog = await _db.DndCatalogs.FirstOrDefaultAsync(c => c.UserId == userId);
            var jsonStr = data.ToString();

            if (catalog == null)
            {
                catalog = new DndCatalog
                {
                    UserId = userId,
                    DataJson = jsonStr
                };
                _db.DndCatalogs.Add(catalog);
            }
            else
            {
                catalog.DataJson = jsonStr;
            }

            await _db.SaveChangesAsync();

            // Оповещаем все сессии пользователя об обновлении общего каталога
            await _hub.Clients.User(userId.ToString()).SendAsync("InvalidateData", "dnd_catalog");

            return Ok(new { success = true });
        }
    }
}