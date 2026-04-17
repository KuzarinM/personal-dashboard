using DashboardApi.Data;
using DashboardApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DashboardApi.Controllers
{
    [Route("api/admin")]
    [ApiController]
    [Authorize] // Внутри методов проверим IsAdmin
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly AuthService _auth;
        private readonly ILogger<AdminController> _logger;

        public AdminController(AppDbContext db, AuthService auth, ILogger<AdminController> logger)
        {
            _db = db;
            _auth = auth;
            _logger = logger;
        }

        private bool IsAdmin()
        {
            var claim = User.FindFirst("isAdmin");
            return claim != null && claim.Value.ToLower() == "true";
        }

        // --- 1. USER MANAGEMENT ---

        [HttpGet("users")]
        public async Task<IActionResult> GetUsers()
        {
            if (!IsAdmin()) return Forbid();

            var users = await _db.Users
                .AsNoTracking()
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.IsAdmin,
                    u.StatusText,
                    u.LastStatusUpdate,
                    DashboardsCount = u.Dashboards.Count
                })
                .ToListAsync();

            return Ok(users);
        }

        [HttpPost("users/{id}/reset-password")]
        public async Task<IActionResult> ResetPassword(int id)
        {
            if (!IsAdmin()) return Forbid();

            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            // Генерируем пароль
            var newPass = Guid.NewGuid().ToString("n").Substring(0, 8);
            user.PasswordHash = _auth.HashPassword(newPass);
            await _db.SaveChangesAsync();

            // Логируем (чтобы админ увидел в консоли/файле)
            _logger.LogWarning($"[ADMIN] Password reset for user '{user.Username}'. NEW PASS: {newPass}");

            // Возвращаем пароль в ответе (для удобства в UI)
            return Ok(new { success = true, newPassword = newPass });
        }

        [HttpDelete("users/{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (!IsAdmin()) return Forbid();

            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            // Защита от самоубийства
            var myId = int.Parse(User.FindFirst("id")?.Value ?? "0");
            if (user.Id == myId) return BadRequest("Cannot delete yourself");

            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // --- 2. SQL CONSOLE (Adminer Lite) ---

        public record SqlDto(string Query);

        [HttpPost("sql")]
        public async Task<IActionResult> ExecuteSql([FromBody] SqlDto dto)
        {
            if (!IsAdmin()) return Forbid();
            if (string.IsNullOrWhiteSpace(dto.Query)) return BadRequest();

            // Очень простая реализация выполнения SQL через ADO.NET внутри EF
            try
            {
                var connection = _db.Database.GetDbConnection();
                await connection.OpenAsync();

                using var command = connection.CreateCommand();
                command.CommandText = dto.Query;

                // Если это SELECT
                if (dto.Query.Trim().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) ||
                    dto.Query.Trim().StartsWith("PRAGMA", StringComparison.OrdinalIgnoreCase)) // SQLite info
                {
                    using var reader = await command.ExecuteReaderAsync();
                    var result = new List<Dictionary<string, object>>();

                    while (await reader.ReadAsync())
                    {
                        var row = new Dictionary<string, object>();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] = reader.GetValue(i);
                        }
                        result.Add(row);
                    }
                    return Ok(result);
                }
                else
                {
                    // UPDATE, DELETE, INSERT...
                    var rowsAffected = await command.ExecuteNonQueryAsync();
                    return Ok(new { message = $"Executed. Rows affected: {rowsAffected}" });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // --- 3. SCHEMA REFLECTION ---
        [HttpGet("schema")]
        public async Task<IActionResult> GetSchema()
        {
            if (!IsAdmin()) return Forbid();

            var schema = new Dictionary<string, List<string>>();
            var connection = _db.Database.GetDbConnection();

            try
            {
                await connection.OpenAsync();

                // 1. Получаем список таблиц
                var tables = new List<string>();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        tables.Add(reader.GetString(0));
                    }
                }

                // 2. Для каждой таблицы получаем колонки
                foreach (var table in tables)
                {
                    var columns = new List<string>();
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
                        using var reader = await cmd.ExecuteReaderAsync();
                        while (await reader.ReadAsync())
                        {
                            // В PRAGMA table_info колонка с именем называется 'name' (индекс 1)
                            // cid, name, type, notnull, dflt_value, pk
                            columns.Add(reader["name"].ToString()!);
                        }
                    }
                    schema.Add(table, columns);
                }

                return Ok(schema);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
            finally
            {
                await connection.CloseAsync();
            }
        }
    }
}
