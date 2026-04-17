namespace DashboardApi.DTOs.Dashboard
{
    public record UpdateDashboardSettingsDto(
        string Title,
        bool IsPublic,
        bool ScheduleEnabled,
        string ScheduleStart,
        string ScheduleEnd,
        string ScheduleDays
    );
}
