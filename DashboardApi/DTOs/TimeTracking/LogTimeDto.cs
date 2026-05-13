namespace DashboardApi.DTOs.TimeTracking
{
    public record LogTimeDto(
        string TaskName,
        string TimeInput,
        List<string>? Tags = null,
        DateTime? Date = null
    );
}
