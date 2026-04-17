using DashboardApi.Data;
using DashboardApi.DTOs.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DashboardApi.Controllers
{
    [Route("api/public")]
    [ApiController]
    public class PublicController : ControllerBase
    {
        private readonly AppDbContext _db;
        public PublicController(AppDbContext db) => _db = db;

        [HttpGet("notes/{guid}")]
        public async Task<IActionResult> GetSharedNote(Guid guid)
        {
            var note = await _db.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.PublicId == guid);

            if (note == null) return NotFound("Note not found or link expired");

            return Ok(new NoteDto(
                note.Id,
                note.Title,
                note.Content,
                false,
                false,
                note.Type,
                note.PublicId
            ));
        }
    }
}
