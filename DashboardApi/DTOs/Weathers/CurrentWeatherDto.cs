namespace DashboardApi.DTOs.Weather
{
    public record CurrentWeatherDto(
        double Temp,
        int Code, // Код погоды (WMO), чтобы фронт рисовал иконку
        string Description,
        double WindSpeed
    );
}
