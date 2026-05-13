namespace DashboardApi.DTOs.TimeTracking
{
    public record DailySummaryDto(
        DateTime Date,
        int TotalMinutes,
        string TotalFormatted,
        List<TaskSummaryDto> Tasks
    );
}
