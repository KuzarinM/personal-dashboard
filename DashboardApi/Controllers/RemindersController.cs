using DashboardApi.Data;
using DashboardApi.Data.Models;
using DashboardApi.DTOs.Reminder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DashboardApi.Controllers
{
    [Route("api/reminders")]
    [ApiController]
    [Authorize]
    public class RemindersController : ControllerBase
    {
        private readonly AppDbContext _db;

        public RemindersController(AppDbContext db) => _db = db;
        private int GetUserId() => int.Parse(User.FindFirst("id")?.Value ?? "0");

        [HttpGet("{dashboardId}")]
        public async Task<IActionResult> GetList(int dashboardId)
        {
            // Проверка доступа (чтение)
            var dash = await _db.Dashboards.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dashboardId);
            if (dash == null) return NotFound();
            if (!dash.IsPublic && dash.UserId != GetUserId()) return Forbid();

            var list = await _db.Reminders
                .Where(r => r.DashboardId == dashboardId)
                .OrderBy(r => r.TargetTime)
                .Select(r => new ReminderDto(r.Id, r.Message, r.TargetTime, r.RecurrenceType, r.RecurrenceIntervalMin))
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost("{dashboardId}")]
        public async Task<IActionResult> Create(int dashboardId, [FromBody] CreateReminderDto dto)
        {
            var dash = await _db.Dashboards.FirstOrDefaultAsync(d => d.Id == dashboardId && d.UserId == GetUserId());
            if (dash == null) return Forbid();

            var rem = new Reminder
            {
                DashboardId = dashboardId,
                Message = dto.Message,
                TargetTime = dto.TargetTime.ToUniversalTime(),

                // ВАЖНО: Маппинг новых полей
                RecurrenceType = dto.RecurrenceType ?? "None", // Дефолт если null
                RecurrenceIntervalMin = dto.RecurrenceIntervalMin
            };

            _db.Reminders.Add(rem);
            await _db.SaveChangesAsync();

            return Ok(new ReminderDto(rem.Id, rem.Message, rem.TargetTime, rem.RecurrenceType, rem.RecurrenceIntervalMin));
        }

        [HttpPost("{id}/ack")]
        public async Task<IActionResult> Acknowledge(int id)
        {
            var rem = await _db.Reminders.Include(r => r.Dashboard).FirstOrDefaultAsync(r => r.Id == id);
            if (rem == null) return NotFound();
            if (rem.Dashboard.UserId != GetUserId()) return Forbid();

            if (rem.RecurrenceType == "None")
            {
                // Одноразовое - удаляем
                _db.Reminders.Remove(rem);
            }
            else if (rem.RecurrenceType == "Daily")
            {
                // Ежедневное - переносим на завтра
                // Добавляем 1 день к текущему таргету
                rem.TargetTime = rem.TargetTime.AddDays(1);
            }
            else if (rem.RecurrenceType == "Interval")
            {
                // Интервальное - добавляем N минут к "сейчас" (или к таргету, если хотим строгости)
                // Лучше к DateTime.UtcNow, чтобы не накапливать просрочку, если сервер лежал
                rem.TargetTime = DateTime.UtcNow.AddMinutes(rem.RecurrenceIntervalMin);
            }

            await _db.SaveChangesAsync();
            return Ok();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var rem = await _db.Reminders.Include(r => r.Dashboard).FirstOrDefaultAsync(r => r.Id == id);
            if (rem == null) return NotFound();
            if (rem.Dashboard.UserId != GetUserId()) return Forbid();

            _db.Reminders.Remove(rem);
            await _db.SaveChangesAsync();
            return Ok();
        }
    }
}
