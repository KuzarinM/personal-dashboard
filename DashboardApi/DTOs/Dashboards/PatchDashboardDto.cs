namespace DashboardApi.DTOs.Dashboard
{
    public record PatchDashboardDto(
        string? Title,
        bool? IsPublic,
        bool? ScheduleEnabled,
        string? ScheduleStart,
        string? ScheduleEnd,
        string? ScheduleDays, // Принимаем "1,2,3" или null
        int? UrgencyCritical,
        int? UrgencyWarning,
        string? WidgetLayout,
        string? HeaderLayout
    );
}
