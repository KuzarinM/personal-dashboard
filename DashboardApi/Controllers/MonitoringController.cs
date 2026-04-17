using DashboardApi.Data;
using DashboardApi.Data.Models;
using DashboardApi.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Net.NetworkInformation;

namespace DashboardApi.Controllers
{
    [Route("api/monitoring")]
    [ApiController]
    [Authorize]
    public class MonitoringController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IHttpClientFactory _httpFactory;
        private readonly IHubContext<NotificationHub> _hub;

        public MonitoringController(AppDbContext db, IHttpClientFactory httpFactory, IHubContext<NotificationHub> hub)
        {
            _db = db;
            _httpFactory = httpFactory;
            _hub = hub;
        }

        private int GetUserId() => int.Parse(User.FindFirst("id")?.Value ?? "0");

        private async Task<bool> CanEdit(int dashboardId)
        {
            var uid = GetUserId();
            var dash = await _db.Dashboards.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dashboardId);
            if (dash == null) return false;
            if (dash.UserId == uid) return true;
            var access = await _db.DashboardAccesses.AsNoTracking().FirstOrDefaultAsync(a => a.DashboardId == dashboardId && a.UserId == uid);
            return access != null && access.Role == "Editor";
        }

        // GET: Получить список
        [HttpGet("{dashboardId}")]
        public async Task<IActionResult> GetList(int dashboardId)
        {
            var dash = await _db.Dashboards.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dashboardId);
            if (dash == null) return NotFound();
            if (!dash.IsPublic && dash.UserId != GetUserId())
            {
                var hasAccess = await _db.DashboardAccesses.AnyAsync(a => a.DashboardId == dashboardId && a.UserId == GetUserId());
                if (!hasAccess) return Forbid();
            }

            var list = await _db.Monitors
                .Where(m => m.DashboardId == dashboardId)
                .OrderBy(m => m.Name)
                .ToListAsync();
            return Ok(list);
        }

        public record CreateMonitorDto(string Name, string Type, string Target, int IntervalMin);

        [HttpPost("{dashboardId}")]
        public async Task<IActionResult> Create(int dashboardId, [FromBody] CreateMonitorDto dto)
        {
            if (!await CanEdit(dashboardId)) return Forbid();

            var item = new MonitorItem
            {
                DashboardId = dashboardId,
                Name = dto.Name,
                Type = dto.Type,
                Target = dto.Target,
                IntervalMin = Math.Max(10, dto.IntervalMin),
                IsActive = true
            };
            _db.Monitors.Add(item);
            await _db.SaveChangesAsync();

            await _hub.Clients.Group(dashboardId.ToString()).SendAsync("InvalidateData", "monitoring");

            return Ok(item);
        }

        [HttpPatch("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] MonitorItem dto)
        {
            var item = await _db.Monitors.FindAsync(id);
            if (item == null) return NotFound();
            if (!await CanEdit(item.DashboardId)) return Forbid();

            item.Name = dto.Name;
            item.Target = dto.Target;
            item.Type = dto.Type;
            item.IntervalMin = Math.Max(10, dto.IntervalMin);
            item.IsActive = dto.IsActive;

            if (item.IsActive) item.LastCheck = null; // Сброс, чтобы сервис проверил скорее

            await _db.SaveChangesAsync();

            await _hub.Clients.Group(id.ToString()).SendAsync("InvalidateData", "monitoring");

            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _db.Monitors.FindAsync(id);
            if (item == null) return NotFound();
            if (!await CanEdit(item.DashboardId)) return Forbid();

            _db.Monitors.Remove(item);
            await _db.SaveChangesAsync();

            await _hub.Clients.Group(id.ToString()).SendAsync("InvalidateData", "monitoring");

            return Ok();
        }

        // --- НОВЫЕ МЕТОДЫ ---

        // 1. Включить/Выключить всё (для отпуска)
        [HttpPost("{dashboardId}/toggle-all")]
        public async Task<IActionResult> ToggleAll(int dashboardId, [FromQuery] bool active)
        {
            if (!await CanEdit(dashboardId)) return Forbid();

            var monitors = await _db.Monitors.Where(m => m.DashboardId == dashboardId).ToListAsync();
            foreach (var m in monitors)
            {
                m.IsActive = active;
                if (active) m.LastCheck = null; // Сброс таймера
            }
            await _db.SaveChangesAsync();
            return Ok(monitors);
        }

        // 2. Принудительная проверка (Manual Scan)
        [HttpPost("{dashboardId}/force-check")]
        public async Task<IActionResult> ForceCheck(int dashboardId)
        {
            // Разрешаем проверку даже Viewer-у (почему нет?), но можно и CanEdit поставить
            // Для безопасности поставим проверку доступа к дашборду
            var dash = await _db.Dashboards.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dashboardId);
            if (dash == null) return NotFound();
            if (!dash.IsPublic && dash.UserId != GetUserId())
            {
                var hasAccess = await _db.DashboardAccesses.AnyAsync(a => a.DashboardId == dashboardId && a.UserId == GetUserId());
                if (!hasAccess) return Forbid();
            }

            var monitors = await _db.Monitors
                .Where(m => m.DashboardId == dashboardId && m.IsActive) // Проверяем только включенные
                .ToListAsync();

            // Запускаем проверки параллельно
            var tasks = monitors.Select(CheckTarget).ToList();
            await Task.WhenAll(tasks);

            await _db.SaveChangesAsync();
            return Ok(monitors);
        }

        // Логика проверки (дублирует BackgroundService, но для контроллера это норм, чтобы ответить сразу)
        private async Task CheckTarget(MonitorItem item)
        {
            item.LastCheck = DateTime.UtcNow;
            try
            {
                if (item.Type == "Ping")
                {
                    using var ping = new Ping();
                    var host = item.Target.Replace("http://", "").Replace("https://", "").Split('/')[0];
                    var reply = await ping.SendPingAsync(host, 2000);
                    item.IsUp = reply.Status == IPStatus.Success;
                    item.ResponseTimeMs = reply.RoundtripTime;
                    item.LastError = item.IsUp ? null : reply.Status.ToString();
                }
                else if (item.Type == "Http")
                {
                    var client = _httpFactory.CreateClient();
                    client.Timeout = TimeSpan.FromSeconds(5); // Быстрый таймаут для ручного скана
                    var url = item.Target.StartsWith("http") ? item.Target : "https://" + item.Target;

                    var sw = System.Diagnostics.Stopwatch.StartNew();
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
                item.LastError = ex.Message;
            }
        }
    }
}