using DashboardApi.DTOs.Category;

namespace DashboardApi.DTOs.Categories
{
    public record UpdateMenuDto(
        List<CategoryContentDto> Categories
    );
}
