using DashboardApi.Data;
using DashboardApi.Data.Models;
using DashboardApi.DTOs;
using DashboardApi.DTOs.Calendars;
using DashboardApi.DTOs.Category;
using DashboardApi.DTOs.Dashboard;
using DashboardApi.DTOs.Dashboards;
using DashboardApi.DTOs.Events;
using DashboardApi.DTOs.Nodes;
using DashboardApi.DTOs.Settings;
using DashboardApi.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DashboardApi.Controllers
{
    [Route("api/dashboards")]
    [ApiController]
    public class DashboardController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<NotificationHub> _hub;

        public DashboardController(AppDbContext db, IHubContext<NotificationHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        private int GetUserId()
        {
            // Безопасное получение ID, если токена нет - вернет 0
            var claim = User.FindFirst("id");
            return claim != null ? int.Parse(claim.Value) : 0;
        }

        // GET: api/dashboards/{id}
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<DashboardViewDto>> GetDashboardView(int id)
        {
            var d = await _db.Dashboards
                .AsNoTracking()
                .Include(d => d.Categories).ThenInclude(c => c.Items)
                .Include(d => d.Calendars)
                .Include(d => d.ManualEvents)
                .Include(d => d.Integrations)
                .Include(d => d.Notes)
                .Include(d => d.User)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (d == null) return NotFound("Dashboard not found");

            // --- ROLE LOGIC ---
            var userId = GetUserId();
            string myRole = "Viewer"; // Default for anonymous or random user on public dash

            if (userId != 0)
            {
                if (d.UserId == userId)
                {
                    myRole = "Owner";
                }
                else
                {
                    // Check collaborators
                    var access = await _db.DashboardAccesses
                        .AsNoTracking()
                        .FirstOrDefaultAsync(a => a.DashboardId == id && a.UserId == userId);

                    if (access != null) myRole = access.Role; // "Editor" or "Viewer"
                }
            }

            // Access Check
            if (!d.IsPublic && myRole == "Viewer" && userId == 0) return Unauthorized("Access denied");
            if (!d.IsPublic && myRole == "Viewer" && d.UserId != userId)
            {
                // Если залогинен, но нет записи в Access и не владелец - запрет
                // (Здесь можно уточнить: если дашборд приватный, Viewer - это только тот, кто явно добавлен как Viewer)
                var hasAccess = await _db.DashboardAccesses.AnyAsync(a => a.DashboardId == id && a.UserId == userId);
                if (!hasAccess && d.UserId != userId) return Forbid();
            }

            var ownerUsername = d.User.Username;

            var accessUsers = await _db.DashboardAccesses
                .Where(a => a.DashboardId == id)
                .Include(a => a.User)
                .Select(a => a.User.Username)
                .ToListAsync();

            var team = new List<string> { ownerUsername };
            team.AddRange(accessUsers);
            team = team.Distinct().ToList();

            // Mapping
            var dto = new DashboardViewDto(
                Id: d.Id,
                Title: d.Title,
                IsPublic: d.IsPublic,
                Schedule: new ScheduleSettingsDto(
                    Enabled: d.ScheduleEnabled,
                    Start: d.ScheduleStart,
                    End: d.ScheduleEnd,
                    Days: string.IsNullOrEmpty(d.ScheduleDays) ? new List<int>() : d.ScheduleDays.Split(',').Select(int.Parse).ToList()
                ),
                Categories: d.Categories.OrderBy(c => c.Order).Select(c => new CategoryViewDto(
                    Title: c.Title,
                    Items: c.Items.Select(i => new ItemViewDto(
                        Name: i.Name,
                        Url: i.Url,
                        UrlLocal: i.UrlLocal,
                        Desc: i.Description,
                        Icon: i.Icon
                    )).ToList()
                )).ToList(),
                Calendars: d.Calendars.Select(cal => new CalendarViewDto(
                    Name: cal.Name,
                    Url: cal.Url,
                    Icon: cal.Icon
                )).ToList(),
                ManualEvents: d.ManualEvents.Select(e => new ManualEventViewDto(
                    Name: e.Name,
                    Date: e.Date,
                    Icon: e.Icon
                )).ToList(),
                Notes: d.Notes.Where(n => !n.IsArchived).Select(n => new NoteDto(n.Id, n.Title, n.Content, n.IsArchived, n.IsPinned, n.Type, n.PublicId)).ToList(),
                ActiveIntegrations: d.Integrations.Select(i => i.Type).ToList(),
                Urgency: new UrgencySettingsDto(d.UrgencyCriticalMin, d.UrgencyWarningMin),
                WidgetLayout: d.WidgetLayout,
                HeaderLayout: d.HeaderLayout,
                MyRole: myRole, // <--- Передаем роль на фронт
                TeamMembers: team
            );

            return Ok(dto);
        }
        [HttpGet("{id}/access")]
        [Authorize]
        public async Task<IActionResult> GetTeam(int id)
        {
            var dash = await _db.Dashboards.FindAsync(id);
            if (dash == null) return NotFound();
            if (dash.UserId != GetUserId()) return Forbid(); // Только владелец видит настройки доступа

            var members = await _db.DashboardAccesses
                .Where(a => a.DashboardId == id)
                .Include(a => a.User)
                .Select(a => new { a.UserId, a.User.Username, a.Role })
                .ToListAsync();

            return Ok(members);
        }

        [HttpDelete("{id}/access/{userId}")]
        [Authorize]
        public async Task<IActionResult> RemoveUser(int id, int userId)
        {
            var dash = await _db.Dashboards.FindAsync(id);
            if (dash == null) return NotFound();
            if (dash.UserId != GetUserId()) return Forbid();

            var access = await _db.DashboardAccesses.FirstOrDefaultAsync(a => a.DashboardId == id && a.UserId == userId);
            if (access != null)
            {
                _db.DashboardAccesses.Remove(access);
                await _db.SaveChangesAsync();
            }
            return Ok(new { success = true });
        }

        // POST: api/dashboards
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CreateDashboardDto dto)
        {
            var entity = new Dashboard
            {
                UserId = GetUserId(),
                Title = dto.Title,
                IsPublic = dto.IsPublic,
                ScheduleEnabled = true // Дефолтные значения
            };

            _db.Dashboards.Add(entity);
            await _db.SaveChangesAsync();

            // Возвращаем ID созданного дашборда
            return CreatedAtAction(nameof(GetDashboardView), new { id = entity.Id }, new { id = entity.Id });
        }

        // PATCH: api/dashboards/{id}
        [HttpPatch("{id}")]
        [Authorize]
        public async Task<IActionResult> PatchDashboard(int id, [FromBody] PatchDashboardDto dto)
        {
            var dash = await _db.Dashboards.FindAsync(id);

            if (dash == null) return NotFound();
            // Проверяем права владельца
            if (dash.UserId != GetUserId()) return Forbid();

            // Применяем изменения, только если они переданы (не null)

            if (dto.Title != null)
                dash.Title = dto.Title;

            if (dto.IsPublic.HasValue)
                dash.IsPublic = dto.IsPublic.Value;

            if (dto.ScheduleEnabled.HasValue)
                dash.ScheduleEnabled = dto.ScheduleEnabled.Value;

            if (dto.ScheduleStart != null)
                dash.ScheduleStart = dto.ScheduleStart;

            if (dto.ScheduleEnd != null)
                dash.ScheduleEnd = dto.ScheduleEnd;

            if (dto.ScheduleDays != null)
                dash.ScheduleDays = dto.ScheduleDays;

            if (dto.UrgencyCritical.HasValue) 
                dash.UrgencyCriticalMin = dto.UrgencyCritical.Value;

            if (dto.UrgencyWarning.HasValue) 
                dash.UrgencyWarningMin = dto.UrgencyWarning.Value;

            if (dto.WidgetLayout != null)
                dash.WidgetLayout = dto.WidgetLayout;

            if (dto.HeaderLayout != null)
                dash.HeaderLayout = dto.HeaderLayout;

            await _db.SaveChangesAsync();

            await _hub.Clients.Group(id.ToString()).SendAsync("InvalidateData", "layout");

            return Ok(new { success = true });
        }

        [HttpGet("list")]
        [Authorize]
        public async Task<IActionResult> GetMyDashboards()
        {
            var userId = GetUserId();

            var list = await _db.Dashboards
                .AsNoTracking()
                // Или я владелец, ИЛИ есть запись в Accesses
                .Where(d => d.UserId == userId || _db.DashboardAccesses.Any(a => a.DashboardId == d.Id && a.UserId == userId))
                .Select(d => new { d.Id, d.Title, d.IsPublic })
                .ToListAsync();

            return Ok(list);
        }

        // GET: api/dashboards/whoami (или просто api/whoami, если роутинг глобальный, но лучше здесь)
        // Чтобы роут совпал с фронтом, добавим атрибут Route
        [HttpGet("/api/whoami")]
        [AllowAnonymous]
        public IActionResult WhoAmI()
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            // Очистка от IPv6-префикса (::ffff:), который часто бывает в Docker/K8s/Localhost
            if (ip.StartsWith("::ffff:")) ip = ip.Substring(7);
            if (ip == "::1") ip = "127.0.0.1";

            // Проверка на локальность
            bool isLocal = false;

            // 1. Локалхост
            if (ip == "127.0.0.1") isLocal = true;

            // 2. Локальные сети (192.168.x.x, 10.x.x.x, 172.16-31.x.x)
            else if (ip.StartsWith("192.168.")) isLocal = true;
            else if (ip.StartsWith("10.")) isLocal = true;
            else if (ip.StartsWith("172."))
            {
                // Простая проверка для диапазона 172.16 - 172.31
                var secondOctet = int.Parse(ip.Split('.')[1]);
                if (secondOctet >= 16 && secondOctet <= 31) isLocal = true;
            }

            return Ok(new { ip, isLocal });
        }

        [HttpGet("/api/status/{dashboardId}")]
        public async Task<IActionResult> GetStatus(int dashboardId)
        {
            // 1. Простейшая проверка доступа (если дашборд не публичный)
            var dash = await _db.Dashboards.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dashboardId);
            if (dash == null) return NotFound();

            if (!dash.IsPublic && dash.UserId != GetUserId()) return Forbid();

            // 2. Ищем статус
            var status = await _db.Statuses.FirstOrDefaultAsync(s => s.DashboardId == dashboardId);

            // 3. Если статуса нет (первый запуск) - возвращаем дефолт
            if (status == null)
            {
                return Ok(new
                {
                    isBreak = false,
                    breakStartTs = 0,
                    totalBreakMs = 0,
                    lastUpdateDate = DateTime.Now // Важно вернуть текущую дату, чтобы фронт не сбросил таймер
                });
            }

            return Ok(status);
        }

        [HttpPost("/api/status/{dashboardId}")]
        [Authorize]
        public async Task<IActionResult> UpdateStatus(int dashboardId, [FromBody] UpdateStatusDto dto)
        {
            // 1. Проверяем владельца
            var dash = await _db.Dashboards.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dashboardId);

            if (dash == null) return NotFound();
            if (dash.UserId != GetUserId()) return Forbid();

            // 2. Ищем статус
            var status = await _db.Statuses.FirstOrDefaultAsync(s => s.DashboardId == dashboardId);

            // Если статуса еще нет - создаем
            if (status == null)
            {
                status = new DashboardStatus { DashboardId = dashboardId };
                _db.Statuses.Add(status);
            }

            // 3. Обновляем поля из DTO
            status.IsBreak = dto.IsBreak;
            status.BreakStartTs = dto.BreakStartTs;
            status.TotalBreakMs = dto.TotalBreakMs;
            status.LastUpdate = dto.LastUpdateDate;

            await _db.SaveChangesAsync();

            return Ok(status);
        }

        [HttpPost("{id}/access")]
        [Authorize]
        public async Task<IActionResult> AddUser(int id, [FromBody] AddUserDto dto)
        {
            var dash = await _db.Dashboards.FindAsync(id);
            if (dash == null) return NotFound();
            if (dash.UserId != GetUserId()) return Forbid();

            // FIX: Ищем без учета регистра (ToLower)
            var targetUser = await _db.Users
                .FirstOrDefaultAsync(u => u.Username.ToLower() == dto.Username.ToLower());

            if (targetUser == null) return BadRequest("User not found");

            // Сами себя не добавляем
            if (targetUser.Id == GetUserId()) return BadRequest("Cannot invite owner");

            if (await _db.DashboardAccesses.AnyAsync(a => a.DashboardId == id && a.UserId == targetUser.Id))
                return BadRequest("User already has access");

            _db.DashboardAccesses.Add(new DashboardAccess
            {
                DashboardId = id,
                UserId = targetUser.Id,
                Role = dto.Role
            });

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }
}
