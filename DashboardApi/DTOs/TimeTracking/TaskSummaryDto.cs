using DashboardApi.Data.Models;

namespace DashboardApi.DTOs.TimeTracking
{
    public record TaskSummaryDto(
        int TaskId,
        string TaskName,
        int Minutes,
        string FormattedTime,
        List<Tag> Tags,
        List<TimeEntryViewDto> Entries
    );
}
