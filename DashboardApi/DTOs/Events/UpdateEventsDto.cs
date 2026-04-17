namespace DashboardApi.DTOs.Events
{
    public record UpdateEventsDto(
        List<ManualEventContentDto> Events
    );
}
