namespace DashboardApi.DTOs.Weather
{
    public record DailyForecastDto(
        DateTime Date,
        double MaxTemp,
        double MinTemp,
        int Code
    );
}
