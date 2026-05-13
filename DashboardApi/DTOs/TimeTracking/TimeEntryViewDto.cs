namespace DashboardApi.DTOs.TimeTracking
{
    public record TimeEntryViewDto(
        int Id,
        int Minutes,
        string FormattedTime
    );
}
