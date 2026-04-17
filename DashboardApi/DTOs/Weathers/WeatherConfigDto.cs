namespace DashboardApi.DTOs.Weather
{
    public record WeatherConfigDto(
        double Latitude,
        double Longitude,
        string CityName
    );
}
