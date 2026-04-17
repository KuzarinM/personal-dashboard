namespace DashboardApi.DTOs.Events
{
    public record ManualEventViewDto(
        string Name,
        DateTime Date,
        string Icon
    );
}
