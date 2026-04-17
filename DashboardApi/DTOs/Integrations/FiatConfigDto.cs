namespace DashboardApi.DTOs.Integrations
{
    public record FiatConfigDto(
        string BaseCurrency,
        List<string> Targets,
        bool UseInverse = false // <-- Новое поле
    );
}
