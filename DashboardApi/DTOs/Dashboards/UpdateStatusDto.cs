namespace DashboardApi.DTOs.Dashboard
{
    public record UpdateStatusDto(
        bool IsBreak,
        long BreakStartTs,
        long TotalBreakMs,
        DateTime LastUpdateDate
    );
}
