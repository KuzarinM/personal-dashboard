namespace DashboardApi.DTOs.Dashboard
{
    public record ItemViewDto(
        string Name,
        string? Url,
        string? UrlLocal,
        string? Desc,
        string? Icon
    );
}
