namespace DashboardApi.DTOs.Events
{
    public record ManualEventContentDto(
        string Name,
        DateTime Date,
        string Icon = "📌"
    );
}
