using System.ComponentModel.DataAnnotations;

namespace DashboardApi.DTOs.Events
{
    public record CreateEventDto(
        [Required] string Name,
        DateTime Date,
        string Icon = "📌"
    );
}
