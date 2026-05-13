using DashboardApi.Data;
using DashboardApi.Data.Models;
using DashboardApi.DTOs.Categories;
using DashboardApi.DTOs.Nodes;
using DashboardApi.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace DashboardApi.Controllers
{
    [Route("api/content")]
    [ApiController]
    [Authorize]
    public class ContentController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<NotificationHub> _hub;

        public ContentController(AppDbContext db, IHubContext<NotificationHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        private int GetUserId() => int.Parse(User.FindFirst("id")?.Value ?? "0");

        // Вспомогательный метод проверки доступа
        private async Task<bool> CheckAccess(int dashboardId)
        {
            var dash = await _db.Dashboards.AsNoTracking().FirstOrDefaultAsync(d => d.Id == dashboardId);
            return dash != null && dash.UserId == GetUserId();
        }

        // PUT: api/content/{dashboardId}/structure
        // Полная перезапись категорий и ссылок
        [HttpPut("{dashboardId}/structure")]
        public async Task<IActionResult> UpdateStructure(int dashboardId, [FromBody] UpdateContentDto dto)
        {
            // 1. Проверяем доступ
            var dash = await _db.Dashboards
                .Include(d => d.Categories).ThenInclude(c => c.Items)
                .FirstOrDefaultAsync(d => d.Id == dashboardId);

            if (dash == null) return NotFound();
            if (dash.UserId != GetUserId()) return Forbid();

            // 2. Очистка старого (Стратегия: Wipe & Replace)
            // Это проще и надежнее для CMS-подобных списков, чем вычислять diff
            _db.Items.RemoveRange(dash.Categories.SelectMany(c => c.Items));
            _db.Categories.RemoveRange(dash.Categories);

            // 3. Создание нового
            int catOrder = 0;
            foreach (var catDto in dto.Categories)
            {
                var newCat = new Category
                {
                    Title = catDto.Title,
                    Order = catOrder++,
                    DashboardId = dashboardId,
                    Items = catDto.Items.Select(i => new Item
                    {
                        Name = i.Name,
                        Url = i.Url,
                        UrlLocal = i.UrlLocal,
                        Description = i.Description,
                        Icon = i.Icon
                    }).ToList()
                };
                _db.Categories.Add(newCat);
            }

            await _db.SaveChangesAsync();

            await _hub.Clients.Group(dashboardId.ToString()).SendAsync("InvalidateData", "content");

            return Ok(new { success = true });
        }

        // --- GET NOTES ---

        // 1. Получить АКТИВНЫЕ заметки (для рабочего стола)
        [HttpGet("{dashboardId}/notes")]
        public async Task<IActionResult> GetActiveNotes(int dashboardId)
        {
            // Можно добавить проверку IsPublic для чтения, если нужно
            var notes = await _db.Notes
                .Where(n => n.DashboardId == dashboardId && !n.IsArchived) // Только НЕ архивные
                .Select(n => new NoteDto(n.Id, n.Title, n.Content, n.IsArchived, n.IsPinned,n.Type, n.PublicId))
                .ToListAsync();
            return Ok(notes);
        }

        // 2. Получить АРХИВНЫЕ заметки (отдельный экран/модалка)
        [HttpGet("{dashboardId}/notes/archive")]
        public async Task<IActionResult> GetArchivedNotes(int dashboardId)
        {
            if (!await CheckAccess(dashboardId)) return Forbid(); // Архив обычно приватный

            var notes = await _db.Notes
                .Where(n => n.DashboardId == dashboardId && n.IsArchived) // Только архивные
                .Select(n => new NoteDto(n.Id, n.Title, n.Content, n.IsArchived, n.IsPinned,  n.Type, n.PublicId))
                .ToListAsync();
            return Ok(notes);
        }

        [HttpPut("{dashboardId}/notes")]
        public async Task<IActionResult> SaveActiveNotes(int dashboardId, [FromBody] List<NoteDto> notesDto)
        {
            if (!await CheckAccess(dashboardId)) return Forbid();

            var existingNotes = await _db.Notes
                .Where(n => n.DashboardId == dashboardId && !n.IsArchived)
                .ToListAsync();

            var processedNotes = new List<Note>();

            foreach (var dto in notesDto)
            {
                Note? note = null;
                if (dto.Id.HasValue && dto.Id.Value > 0)
                    note = existingNotes.FirstOrDefault(n => n.Id == dto.Id.Value);

                if (note == null)
                {
                    note = new Note { DashboardId = dashboardId, IsArchived = false };
                    _db.Notes.Add(note);
                }

                note.Title = dto.Title;
                note.Content = dto.Content;
                note.IsPinned = dto.IsPinned;
                note.PublicId = dto.PublicId;

                // Сохраняем Тип
                note.Type = dto.Type ?? "Text";

                // PublicId не меняем здесь, для этого отдельные методы

                processedNotes.Add(note);
            }

            var notesToDelete = existingNotes.Where(en => !processedNotes.Contains(en)).ToList();
            if (notesToDelete.Any()) _db.Notes.RemoveRange(notesToDelete);

            await _db.SaveChangesAsync();

            // Возвращаем DTO
            var result = processedNotes.Select(n => new NoteDto(n.Id, n.Title, n.Content, n.IsArchived, n.IsPinned, n.Type, n.PublicId)).ToList();
            return Ok(result);
        }

        // --- SHARING ---

        [HttpPost("{dashboardId}/notes/{noteId}/share")]
        public async Task<IActionResult> ShareNote(int dashboardId, int noteId)
        {
            if (!await CheckAccess(dashboardId)) return Forbid();

            var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == noteId);
            if (note == null) return NotFound();

            // Генерируем GUID, если его нет
            if (note.PublicId == null) note.PublicId = Guid.NewGuid();

            await _db.SaveChangesAsync();
            return Ok(new { publicId = note.PublicId });
        }

        [HttpDelete("{dashboardId}/notes/{noteId}/share")]
        public async Task<IActionResult> UnshareNote(int dashboardId, int noteId)
        {
            if (!await CheckAccess(dashboardId)) return Forbid();

            var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == noteId);
            if (note == null) return NotFound();

            note.PublicId = null; // Закрываем доступ
            await _db.SaveChangesAsync();
            return Ok();
        }

        // --- ACTIONS (Archive / Restore / Delete) ---

        // 4. Отправить в архив
        [HttpPost("{dashboardId}/notes/{noteId}/archive")]
        public async Task<IActionResult> ArchiveNote(int dashboardId, int noteId)
        {
            if (!await CheckAccess(dashboardId)) return Forbid();

            var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.DashboardId == dashboardId);
            if (note == null) return NotFound();

            note.IsArchived = true; // Просто меняем флаг
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // 5. Восстановить из архива
        [HttpPost("{dashboardId}/notes/{noteId}/restore")]
        public async Task<IActionResult> RestoreNote(int dashboardId, int noteId)
        {
            if (!await CheckAccess(dashboardId)) return Forbid();

            var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.DashboardId == dashboardId);
            if (note == null) return NotFound();

            note.IsArchived = false; // Возвращаем в строй
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // 6. Удалить навсегда (например, из архива)
        [HttpDelete("{dashboardId}/notes/{noteId}")]
        public async Task<IActionResult> DeleteNote(int dashboardId, int noteId)
        {
            if (!await CheckAccess(dashboardId)) return Forbid();

            var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == noteId && n.DashboardId == dashboardId);
            if (note == null) return NotFound();

            _db.Notes.Remove(note);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }
}
