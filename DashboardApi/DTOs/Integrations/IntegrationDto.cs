namespace DashboardApi.DTOs.Integrations
{
    public record IntegrationDto(
        string Type,       // "Telegram"
        object Config      // JSON объект настроек
    );
}
