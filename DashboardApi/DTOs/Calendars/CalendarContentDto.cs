namespace DashboardApi.DTOs.Calendars
{
    public record CalendarContentDto(
        string Name,
        string Url,
        string Icon = "📅"
    );
}
