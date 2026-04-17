namespace DashboardApi.DTOs.Weather
{
    public record WeatherDataDto(
        CurrentWeatherDto Current,
        List<DailyForecastDto> Daily
    );

}
