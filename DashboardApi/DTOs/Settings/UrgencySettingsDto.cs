namespace DashboardApi.DTOs.Settings
{
    public record UrgencySettingsDto(
        int Critical, // в минутах
        int Warning   // в минутах
    );
}
