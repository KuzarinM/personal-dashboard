namespace DashboardApi.DTOs.Dashboard
{
    public record ItemContentDto(
        string Name,
        string? Url,
        string? UrlLocal,
        string? Description,
        string? Icon
    );
}
