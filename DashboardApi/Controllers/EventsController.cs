using DashboardApi.Data;
using DashboardApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DashboardApi.Controllers
{
    [Route("api/events")]
    [ApiController]
    public class EventsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly CalendarAggregatorService _service;

        public EventsController(AppDbContext db, CalendarAggregatorService service)
        {
            _db = db;
            _service = service;
        }

        // Теперь принимаем int id
        [HttpGet("{id}")]
        public async Task<IActionResult> GetEvents(int id)
        {
            var dash = await _db.Dashboards
                .Include(d => d.Calendars)
                .Include(d => d.ManualEvents)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (dash == null) return NotFound();

            // Тут можно добавить проверку на IsPublic, если нужно закрыть календарь от посторонних
            // if (!dash.IsPublic && dash.UserId != GetUserId()) return Unauthorized();

            var events = await _service.GetEventsAsync(dash.Calendars, dash.ManualEvents);
            return Ok(events);
        }
    }
}
