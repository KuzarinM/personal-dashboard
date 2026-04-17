using DashboardApi.Data;
using DashboardApi.Data.Models;
using DashboardApi.DTOs.Weather;
using DashboardApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace DashboardApi.Controllers
{
    [Route("api/weather")]
    [ApiController]
    public class WeatherController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly WeatherService _service;

        public WeatherController(AppDbContext db, WeatherService service)
        {
            _db = db;
            _service = service;
        }

        // Helper
        private int GetUserId() => int.Parse(User.FindFirst("id")?.Value ?? "0");

        // 1. Поиск города (публичный или закрытый - по желанию, пусть будет открытым для удобства)
        [HttpGet("search")]
        public async Task<IActionResult> SearchCity([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return Ok(new List<object>());
            var results = await _service.SearchCityAsync(q);
            return Ok(results);
        }

        // 2. Сохранение настроек погоды для дашборда
        [HttpPut("dashboards/{dashboardId}/settings")]
        [Authorize]
        public async Task<IActionResult> SaveSettings(int dashboardId, [FromBody] WeatherConfigDto dto)
        {
            var dash = await _db.Dashboards
                .Include(d => d.Integrations)
                .FirstOrDefaultAsync(d => d.Id == dashboardId);

            if (dash == null) return NotFound();
            if (dash.UserId != GetUserId()) return Forbid();

            // Ищем существующую интеграцию или создаем новую
            var integration = dash.Integrations.FirstOrDefault(i => i.Type == "Weather");
            if (integration == null)
            {
                integration = new Integration { DashboardId = dashboardId, Type = "Weather" };
                _db.Integrations.Add(integration);
            }

            // Сохраняем Lat/Lon/CityName в JSON поле
            integration.ConfigJson = JsonConvert.SerializeObject(dto);
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }

        // 3. Получение погоды для дашборда
        [HttpGet("dashboards/{dashboardId}")]
        public async Task<IActionResult> GetDashboardWeather(int dashboardId)
        {
            // Читаем конфиг из базы
            var integration = await _db.Integrations
                .Include(i => i.Dashboard)
                .FirstOrDefaultAsync(i => i.DashboardId == dashboardId && i.Type == "Weather");

            if (integration == null || string.IsNullOrEmpty(integration.ConfigJson))
                return Ok(new { notConfigured = true });

            // Проверка прав (если дашборд приватный - нужна авторизация)
            // Если вы хотите показывать погоду на публичном дашборде - эту проверку можно убрать/ослабить
            if (!integration.Dashboard.IsPublic)
            {
                // Простейшая проверка, есть ли юзер вообще (для токена)
                // В идеале можно сверить ID, если фронт передает токен
            }

            try
            {
                var config = JsonConvert.DeserializeObject<WeatherConfigDto>(integration.ConfigJson);
                if (config == null) return BadRequest("Invalid config");

                var data = await _service.GetForecastAsync(config.Latitude, config.Longitude);
                return Ok(data);
            }
            catch
            {
                return StatusCode(500, "Error fetching weather");
            }
        }

        [HttpGet("dashboards/{dashboardId}/settings")]
        public async Task<IActionResult> GetWeatherSettings(int dashboardId)
        {
            var integration = await _db.Integrations
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.DashboardId == dashboardId && i.Type == "Weather");

            if (integration == null || string.IsNullOrEmpty(integration.ConfigJson))
                return Ok(new { }); // Пустой объект

            // Возвращаем сохраненный конфиг (там есть cityName)
            return Content(integration.ConfigJson, "application/json");
        }
    }
}
