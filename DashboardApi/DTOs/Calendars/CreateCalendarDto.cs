using System.ComponentModel.DataAnnotations;

namespace DashboardApi.DTOs.Calendars
{
    public record CreateCalendarDto(
        [Required] string Name,
        [Required] string Url,
        string Icon = "📅"
    );
}
