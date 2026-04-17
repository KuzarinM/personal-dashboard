using System.ComponentModel.DataAnnotations;

namespace DashboardApi.DTOs.Dashboard
{
    public record CreateItemDto(
        [Required] 
        string Name,
        string? Url,
        string? UrlLocal,
        string? Description,
        string? Icon,
        int CategoryId
    );
}
