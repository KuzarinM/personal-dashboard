using DashboardApi.Data;
using DashboardApi.Data.Models;
using DashboardApi.DTOs.Email;
using DashboardApi.DTOs.Integrations;
using DashboardApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace DashboardApi.Controllers
{
    [Route("api/integrations")]
    [ApiController]
    public class IntegrationsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TelegramService _tgService;
        private readonly EmailService _emailService;
        public IntegrationsController(AppDbContext db, TelegramService tgService, EmailService emailService)
        {
            _db = db;
            _tgService = tgService;
            _emailService = emailService;
        }

        private int GetUserId() => int.Parse(User.FindFirst("id")?.Value ?? "0");

        // DTO для входа
        public record TgLoginDto(string Phone, string? Code, string? Password);

        // STEP 1: Отправить код
        [HttpPost("{dashboardId}/telegram/login-start")]
        [Authorize]
        public async Task<IActionResult> StartLogin(int dashboardId, [FromBody] TgLoginDto dto)
        {
            var dash = await _db.Dashboards.FirstOrDefaultAsync(d => d.Id == dashboardId && d.UserId == GetUserId());
            if (dash == null) return Forbid();

            try
            {
                // Это запустит процесс и отправит код в Телеграм
                var status = await _tgService.StartAuthAsync(dashboardId, dto.Phone);
                return Ok(new { status });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        // STEP 2: Ввести код (и пароль, если нужен)
        [HttpPost("{dashboardId}/telegram/login-complete")]
        [Authorize]
        public async Task<IActionResult> CompleteLogin(int dashboardId, [FromBody] TgLoginDto dto)
        {
            var dash = await _db.Dashboards.FirstOrDefaultAsync(d => d.Id == dashboardId && d.UserId == GetUserId());
            if (dash == null) return Forbid();

            try
            {
                if (string.IsNullOrEmpty(dto.Code)) return BadRequest("Code required");

                var status = await _tgService.CompleteAuthAsync(dashboardId, dto.Code, dto.Password);

                if (status == "SUCCESS") return Ok(new { success = true });

                // Если вернулось что-то другое (например "password_needed")
                return Ok(new { status, needsPassword = true });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("{dashboardId}/telegram/messages")]
        public async Task<IActionResult> GetTgMessages(int dashboardId)
        {
            // Просто проверяем, есть ли запись в БД (флаг включения)
            var integration = await _db.Integrations
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.DashboardId == dashboardId && i.Type == "Telegram");

            if (integration == null) return Ok(new { notConfigured = true });

            // ВАЖНО: Мы больше не передаем session string, сервис сам берет файл
            var result = await _tgService.GetUnreadMessagesAsync(dashboardId);
            return Ok(result);
        }

        // --- CRYPTO ---

        [HttpGet("{dashboardId}/crypto/settings")]
        public async Task<IActionResult> GetCryptoSettings(int dashboardId)
        {
            var integration = await _db.Integrations
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.DashboardId == dashboardId && i.Type == "Crypto");

            if (integration == null || string.IsNullOrEmpty(integration.ConfigJson))
            {
                // Дефолтные настройки, если ничего не сохраняли
                return Ok(new CryptoConfigDto(new List<string> { "bitcoin", "ethereum", "the-open-network" }));
            }

            try
            {
                var config = JsonConvert.DeserializeObject<CryptoConfigDto>(integration.ConfigJson);
                return Ok(config);
            }
            catch
            {
                return Ok(new CryptoConfigDto(new List<string>()));
            }
        }

        [HttpPut("{dashboardId}/crypto/settings")]
        [Authorize]
        public async Task<IActionResult> SaveCryptoSettings(int dashboardId, [FromBody] CryptoConfigDto dto)
        {
            var dash = await _db.Dashboards.FirstOrDefaultAsync(d => d.Id == dashboardId && d.UserId == GetUserId());
            if (dash == null) return Forbid();

            var integration = await _db.Integrations.FirstOrDefaultAsync(i => i.DashboardId == dashboardId && i.Type == "Crypto");
            if (integration == null)
            {
                integration = new Integration { DashboardId = dashboardId, Type = "Crypto" };
                _db.Integrations.Add(integration);
            }

            integration.ConfigJson = JsonConvert.SerializeObject(dto);
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }

    // --- FIAT (FOREX) ---

    [HttpGet("{dashboardId}/fiat/settings")]
        public async Task<IActionResult> GetFiatSettings(int dashboardId)
        {
            var integration = await _db.Integrations
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.DashboardId == dashboardId && i.Type == "Fiat");

            if (integration == null || string.IsNullOrEmpty(integration.ConfigJson))
            {
                // Дефолт: База USD, следим за EUR и RUB
                return Ok(new FiatConfigDto("USD", new List<string> { "EUR", "RUB" }));
            }

            try
            {
                var config = JsonConvert.DeserializeObject<FiatConfigDto>(integration.ConfigJson);
                return Ok(config);
            }
            catch
            {
                return Ok(new FiatConfigDto("USD", new List<string>()));
            }
        }

        [HttpPut("{dashboardId}/fiat/settings")]
        [Authorize]
        public async Task<IActionResult> SaveFiatSettings(int dashboardId, [FromBody] FiatConfigDto dto)
        {
            var dash = await _db.Dashboards.FirstOrDefaultAsync(d => d.Id == dashboardId && d.UserId == GetUserId());
            if (dash == null) return Forbid();

            var integration = await _db.Integrations.FirstOrDefaultAsync(i => i.DashboardId == dashboardId && i.Type == "Fiat");
            if (integration == null)
            {
                integration = new Integration { DashboardId = dashboardId, Type = "Fiat" };
                _db.Integrations.Add(integration);
            }

            integration.ConfigJson = JsonConvert.SerializeObject(dto);
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }
        // --- EMAIL ---

        [HttpGet("{dashboardId}/email/settings")]
        public async Task<IActionResult> GetEmailSettings(int dashboardId)
        {
            var integration = await _db.Integrations.AsNoTracking()
                .FirstOrDefaultAsync(i => i.DashboardId == dashboardId && i.Type == "Email");

            if (integration == null || string.IsNullOrEmpty(integration.ConfigJson))
                return Ok(new EmailConfigDto("", 993, true, "", ""));

            return Ok(JsonConvert.DeserializeObject<EmailConfigDto>(integration.ConfigJson));
        }

        [HttpPut("{dashboardId}/email/settings")]
        [Authorize]
        public async Task<IActionResult> SaveEmailSettings(int dashboardId, [FromBody] EmailConfigDto dto)
        {
            var dash = await _db.Dashboards.FirstOrDefaultAsync(d => d.Id == dashboardId && d.UserId == GetUserId());
            if (dash == null) return Forbid();

            var integration = await _db.Integrations.FirstOrDefaultAsync(i => i.DashboardId == dashboardId && i.Type == "Email");
            if (integration == null)
            {
                integration = new Integration { DashboardId = dashboardId, Type = "Email" };
                _db.Integrations.Add(integration);
            }

            integration.ConfigJson = JsonConvert.SerializeObject(dto);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpGet("{dashboardId}/email/messages")]
        public async Task<IActionResult> GetEmails(int dashboardId)
        {
            // Этот метод может быть медленным (IMAP connect), но для виджета это ок
            var result = await _emailService.GetUnreadEmailsAsync(dashboardId);
            return Ok(result);
        }
    }

}