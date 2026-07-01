using DashboardApi.Data;
using DashboardApi.Data.Models;
using DashboardApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DashboardApi.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly AuthService _auth;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AppDbContext db, AuthService auth, ILogger<AuthController> logger)
        {
            _db = db;
            _auth = auth;
            _logger = logger;
        }

        public record LoginDto(string Username, string Password);

        // DTO для сброса
        public record ResetPasswordDto(string Username);

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);
            if (user == null || !_auth.VerifyPassword(dto.Password, user.PasswordHash))
                return Unauthorized("Invalid credentials");

            var token = _auth.GenerateToken(user);
            return Ok(new { token, user.Username, user.IsAdmin });
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { error = "Username and password required" });

            if (await _db.Users.AnyAsync(u => u.Username == dto.Username))
                return BadRequest(new { error = "User already exists" });

            var user = new User
            {
                Username = dto.Username,
                PasswordHash = _auth.HashPassword(dto.Password),
                IsAdmin = false, // Новые юзеры всегда не админы
                StatusText = "Newbie",
                StatusEmoji = "👋",
                StatusColor = "zinc",
                LastStatusUpdate = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync(); // Сохраняем пользователя, чтобы получить его ID

            // --- АВТОМАТИЧЕСКОЕ СОЗДАНИЕ ДЕФОЛТНОГО ДАШБОРДА ДЛЯ НОВОГО ЮЗЕРА ---
            var defaultDashboard = new Dashboard
            {
                UserId = user.Id,
                Title = "MAIN_DASHBOARD",
                IsPublic = false,
                ScheduleEnabled = true,
                ScheduleStart = "10:00",
                ScheduleEnd = "19:00",
                ScheduleDays = "1,2,3,4,5",
                WidgetLayout = "",
                HeaderLayout = ""
            };

            _db.Dashboards.Add(defaultDashboard);
            await _db.SaveChangesAsync();

            return Ok(new { message = "Registration successful" });
        }

        // --- НОВЫЙ МЕТОД: СБРОС ПАРОЛЯ ---
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == dto.Username);
            if (user == null)
                return NotFound("User not found");

            // 1. Генерируем временный пароль (8 символов)
            var newPassword = Guid.NewGuid().ToString("n").Substring(0, 8);

            // 2. Хешируем и сохраняем
            user.PasswordHash = _auth.HashPassword(newPassword);
            await _db.SaveChangesAsync();

            // 3. ПИШЕМ В ЛОГИ (Уровень Warning, чтобы пробилось через фильтры в консоль)
            _logger.LogWarning($"!!! PASSWORD RESET !!! User: {user.Username} | New Password: {newPassword}");

            // В ответе пароль НЕ отдаем (имитация безопасности), говорим смотреть логи
            return Ok(new { message = "Password reset. Check server logs (console or file)." });
        }
    }
}
