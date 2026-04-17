namespace DashboardApi.DTOs.Settings
{
    public record ScheduleSettingsDto(
        bool Enabled,
        string Start,
        string End,
        List<int> Days
    );
}
