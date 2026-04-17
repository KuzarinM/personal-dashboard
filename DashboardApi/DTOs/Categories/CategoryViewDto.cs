using DashboardApi.DTOs.Dashboard;

namespace DashboardApi.DTOs.Category
{
    public record CategoryViewDto(
        string Title,
        List<ItemViewDto> Items
    );

}
