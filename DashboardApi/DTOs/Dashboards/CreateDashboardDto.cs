using System.ComponentModel.DataAnnotations;

namespace DashboardApi.DTOs.Dashboard
{
    public record CreateDashboardDto(
        [Required] 
        string Title, // Slug больше не нужен
        bool IsPublic = false
    );
}
