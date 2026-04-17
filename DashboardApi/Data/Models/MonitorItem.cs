using Newtonsoft.Json;

namespace DashboardApi.Data.Models
{
    public class MonitorItem
    {
        public int Id { get; set; }

        public string Name { get; set; } = "Server";
        public string Type { get; set; } = "Ping"; // "Ping" or "Http"
        public string Target { get; set; } = "127.0.0.1"; // IP or URL

        public int IntervalMin { get; set; } = 10; // Мин 10 минут
        public bool IsActive { get; set; } = true;

        // Status State
        public bool IsUp { get; set; }
        public DateTime? LastCheck { get; set; }
        public long ResponseTimeMs { get; set; }
        public string? LastError { get; set; }

        public int DashboardId { get; set; }
        [JsonIgnore]
        public Dashboard Dashboard { get; set; } = null!;
    }
}
