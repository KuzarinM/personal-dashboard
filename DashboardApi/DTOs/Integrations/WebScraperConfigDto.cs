namespace DashboardApi.DTOs.Integrations
{
    public class WebScraperConfigDto
    {
        // Промпт, описывающий текущие интересы, цели и пожелания пользователя
        public string InterestsPrompt { get; set; } = string.Empty;

        public List<WebScraperTargetDto> Targets { get; set; } = new List<WebScraperTargetDto>();
    }

    public class WebScraperTargetDto
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;

        // Тип источника: "HTML" или "RSS"
        public string TargetType { get; set; } = "HTML";

        public bool Enabled { get; set; } = true;
    }
}
