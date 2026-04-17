namespace DashboardApi.DTOs.Weather
{
    public record CitySearchResultDto(
        string Name,
        double Latitude,
        double Longitude,
        string Country,
        string Admin1 // Область/Регион
    );
}
