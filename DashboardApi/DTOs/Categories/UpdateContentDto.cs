using DashboardApi.DTOs.Category;

namespace DashboardApi.DTOs.Categories
{
    public record UpdateContentDto(
        List<CategoryContentDto> Categories
    );
}
