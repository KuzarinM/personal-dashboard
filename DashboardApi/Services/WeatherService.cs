using DashboardApi.DTOs.Weather;
using System.Globalization;
using System.Text.Json;

namespace DashboardApi.Services
{
    public class WeatherService
    {
        private readonly HttpClient _http;

        public WeatherService(HttpClient http)
        {
            _http = http;
        }

        // 1. Поиск города (чтобы юзер мог ввести "Moscow" и получить координаты)
        public async Task<List<CitySearchResultDto>> SearchCityAsync(string query)
        {
            try
            {
                var url = $"https://geocoding-api.open-meteo.com/v1/search?name={query}&count=5&language=ru&format=json";
                var response = await _http.GetStringAsync(url);

                using var doc = JsonDocument.Parse(response);
                if (!doc.RootElement.TryGetProperty("results", out var results))
                    return new List<CitySearchResultDto>();

                var list = new List<CitySearchResultDto>();
                foreach (var el in results.EnumerateArray())
                {
                    list.Add(new CitySearchResultDto(
                        Name: el.GetProperty("name").GetString() ?? "",
                        Latitude: el.GetProperty("latitude").GetDouble(),
                        Longitude: el.GetProperty("longitude").GetDouble(),
                        Country: el.TryGetProperty("country", out var c) ? c.GetString() ?? "" : "",
                        Admin1: el.TryGetProperty("admin1", out var a) ? a.GetString() ?? "" : ""
                    ));
                }
                return list;
            }
            catch
            {
                return new List<CitySearchResultDto>();
            }
        }

        // 2. Получение прогноза
        public async Task<WeatherDataDto?> GetForecastAsync(double lat, double lon)
        {
            try
            {
                // Запрашиваем: текущую погоду + дневной прогноз на 3 дня
                var latStr = lat.ToString(CultureInfo.InvariantCulture);
                var lonStr = lon.ToString(CultureInfo.InvariantCulture);
                var url = $"https://api.open-meteo.com/v1/forecast?latitude={latStr}&longitude={lonStr}&current=temperature_2m,weather_code,wind_speed_10m&daily=weather_code,temperature_2m_max,temperature_2m_min&timezone=auto&forecast_days=4";

                var json = await _http.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Парсим текущую погоду
                var current = root.GetProperty("current");
                var currentDto = new CurrentWeatherDto(
                    Temp: current.GetProperty("temperature_2m").GetDouble(),
                    Code: current.GetProperty("weather_code").GetInt32(),
                    Description: WmoCodeToString(current.GetProperty("weather_code").GetInt32()),
                    WindSpeed: current.GetProperty("wind_speed_10m").GetDouble()
                );

                // Парсим прогноз по дням
                var daily = root.GetProperty("daily");
                var timeArr = daily.GetProperty("time").EnumerateArray().ToList();
                var codeArr = daily.GetProperty("weather_code").EnumerateArray().ToList();
                var maxArr = daily.GetProperty("temperature_2m_max").EnumerateArray().ToList();
                var minArr = daily.GetProperty("temperature_2m_min").EnumerateArray().ToList();

                var dailyList = new List<DailyForecastDto>();
                // Пропускаем 0-й индекс (сегодня), берем следующие 3 дня, или берем все - как хотите
                for (int i = 0; i < timeArr.Count; i++)
                {
                    dailyList.Add(new DailyForecastDto(
                        Date: DateTime.Parse(timeArr[i].GetString()!),
                        MaxTemp: maxArr[i].GetDouble(),
                        MinTemp: minArr[i].GetDouble(),
                        Code: codeArr[i].GetInt32()
                    ));
                }

                return new WeatherDataDto(currentDto, dailyList);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Weather API Error: {ex.Message}");
                return null;
            }
        }

        // Расшифровка кодов Open-Meteo (WMO)
        private string WmoCodeToString(int code)
        {
            return code switch
            {
                0 => "Ясно",
                1 => "Преимущественно ясно",
                2 => "Переменная облачность",
                3 => "Пасмурно",
                45 or 48 => "Туман",
                51 or 53 or 55 => "Морось",
                61 or 63 or 65 => "Дождь",
                71 or 73 or 75 => "Снег",
                80 or 81 or 82 => "Ливень",
                95 or 96 or 99 => "Гроза",
                _ => "Неизвестно"
            };
        }
    }
}
