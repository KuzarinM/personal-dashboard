using DashboardApi.Data;
using DashboardApi.Data.Models;
using DashboardApi.DTOs.Calendars;
using DashboardApi.DTOs.Events;
using DashboardApi.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DashboardApi.Controllers
{
    [Route("api/dashboards/{dashboardId}/resources")] // Путь теперь включает ID дашборда
    [ApiController]
    [Authorize]
    public class ResourcesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<NotificationHub> _hub;

        public ResourcesController(AppDbContext db, IHubContext<NotificationHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        private int GetUserId() => int.Parse(User.FindFirst("id")?.Value ?? "0");

        // ==========================================
        // 1. КАЛЕНДАРИ
        // ==========================================

        [HttpPut("calendars")]
        public async Task<IActionResult> UpdateCalendars(int dashboardId, [FromBody] UpdateCalendarsDto dto)
        {
            var dash = await _db.Dashboards.Include(d => d.Calendars).FirstOrDefaultAsync(d => d.Id == dashboardId);

            if (dash == null) return NotFound();
            if (dash.UserId != GetUserId()) return Forbid();

            // Wipe & Replace
            _db.Calendars.RemoveRange(dash.Calendars);

            foreach (var cal in dto.Calendars)
            {
                _db.Calendars.Add(new CalendarSource
                {
                    DashboardId = dashboardId,
                    Name = cal.Name,
                    Url = cal.Url,
                    Icon = cal.Icon
                });
            }

            await _db.SaveChangesAsync();

            await _hub.Clients.Group(dashboardId.ToString()).SendAsync("InvalidateData", "calendar");

            return Ok(new { success = true });
        }

        // ==========================================
        // 2. РУЧНЫЕ СОБЫТИЯ
        // ==========================================

        [HttpPut("manual-events")]
        public async Task<IActionResult> UpdateManualEvents(int dashboardId, [FromBody] UpdateEventsDto dto)
        {
            var dash = await _db.Dashboards.Include(d => d.ManualEvents).FirstOrDefaultAsync(d => d.Id == dashboardId);

            if (dash == null) return NotFound();
            if (dash.UserId != GetUserId()) return Forbid();

            // Wipe & Replace
            _db.ManualEvents.RemoveRange(dash.ManualEvents);

            foreach (var evt in dto.Events)
            {
                _db.ManualEvents.Add(new ManualEvent
                {
                    DashboardId = dashboardId,
                    Name = evt.Name,
                    Date = evt.Date,
                    Icon = evt.Icon
                });
            }

            await _db.SaveChangesAsync();

            await _hub.Clients.Group(dashboardId.ToString()).SendAsync("InvalidateData", "calendar");

            return Ok(new { success = true });
        }
    }
}
