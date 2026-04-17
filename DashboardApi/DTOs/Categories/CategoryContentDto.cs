using DashboardApi.DTOs.Dashboard;

namespace DashboardApi.DTOs.Category
{
    public record CategoryContentDto(
        string Title,
        List<ItemContentDto> Items
    );
}
