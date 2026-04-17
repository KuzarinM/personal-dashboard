using DashboardApi.Data;
using DashboardApi.Data.Models;
using DashboardApi.DTOs.Nodes; // Убедись, что NoteDto доступен
using DashboardApi.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DashboardApi.Controllers
{
    [Route("api/notes")]
    [ApiController]
    [Authorize]
    public class NotesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<NotificationHub> _hub;
        public NotesController(AppDbContext db, IHubContext<NotificationHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        private int GetUserId() => int.Parse(User.FindFirst("id")?.Value ?? "0");

        // Helper: Проверка прав (Владелец или Редактор)
        private async Task<bool> CanEdit(int dashboardId)
        {
            var uid = GetUserId();
            var dash = await _db.Dashboards.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dashboardId);
            if (dash == null) return false;
            if (dash.UserId == uid) return true; // Owner

            var access = await _db.DashboardAccesses
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.DashboardId == dashboardId && a.UserId == uid);
            return access != null && access.Role == "Editor";
        }

        public record CreateNoteDto(int DashboardId, string Title, string Type);
        public record UpdateNoteDto(string? Title, string? Content, bool? IsPinned, bool? IsArchived, string? Type);

        // GET: api/notes/archive/{dashboardId} (НОВЫЙ МЕТОД)
        [HttpGet("archive/{dashboardId}")]
        public async Task<IActionResult> GetArchived(int dashboardId)
        {
            // Архив видят только те, кто имеет доступ к дашборду.
            // Можно использовать CanEdit, а можно разрешить и Viewer-ам (зависит от логики).
            // Допустим, архив приватный, только для редакторов:
            if (!await CanEdit(dashboardId)) return Forbid();

            var notes = await _db.Notes
                .AsNoTracking()
                .Where(n => n.DashboardId == dashboardId && n.IsArchived)
                .OrderByDescending(n => n.Id)
                .Select(n => new NoteDto(n.Id, n.Title, n.Content, n.IsArchived, n.IsPinned, n.Type, n.PublicId))
                .ToListAsync();

            return Ok(notes);
        }

        // POST: api/notes
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateNoteDto dto)
        {
            if (!await CanEdit(dto.DashboardId)) return Forbid();

            var note = new Note
            {
                DashboardId = dto.DashboardId,
                Title = dto.Title,
                Type = dto.Type ?? "Text",
                Content = "",
                IsPinned = false,
                IsArchived = false
            };

            _db.Notes.Add(note);
            await _db.SaveChangesAsync();

            await _hub.Clients.Group(dto.DashboardId.ToString()).SendAsync("InvalidateData", "notes");

            // Возвращаем DTO
            return Ok(new NoteDto(note.Id, note.Title, note.Content, note.IsArchived, note.IsPinned, note.Type, null));
        }

        // PATCH: api/notes/{id}
        [HttpPatch("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateNoteDto dto)
        {
            var note = await _db.Notes.FindAsync(id);
            if (note == null) return NotFound();

            if (!await CanEdit(note.DashboardId)) return Forbid();

            if (dto.Title != null) note.Title = dto.Title;
            if (dto.Content != null) note.Content = dto.Content;
            if (dto.IsPinned.HasValue) note.IsPinned = dto.IsPinned.Value;
            if (dto.IsArchived.HasValue) note.IsArchived = dto.IsArchived.Value;
            if (dto.Type != null) note.Type = dto.Type;

            await _db.SaveChangesAsync();

            await _hub.Clients.Group(note.DashboardId.ToString()).SendAsync("InvalidateData", "notes");

            return Ok(new { success = true });
        }

        // DELETE: api/notes/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var note = await _db.Notes.FindAsync(id);
            if (note == null) return NotFound();

            if (!await CanEdit(note.DashboardId)) return Forbid();

            _db.Notes.Remove(note);
            await _db.SaveChangesAsync();

            await _hub.Clients.Group(note.DashboardId.ToString()).SendAsync("InvalidateData", "notes");

            return Ok(new { success = true });
        }
    }
}