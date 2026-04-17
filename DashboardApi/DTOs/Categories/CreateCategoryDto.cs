using System.ComponentModel.DataAnnotations;

namespace DashboardApi.DTOs.Category
{
    public record CreateCategoryDto(
        [Required] 
        string Title,
        int Order
    );
}
