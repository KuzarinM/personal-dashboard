namespace DashboardApi.DTOs.Integrations
{
    public record CryptoConfigDto(
        List<string> Coins // Список ID: ["bitcoin", "ethereum", "the-open-network"]
    );
}
