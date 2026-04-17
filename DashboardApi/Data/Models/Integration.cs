using Newtonsoft.Json;

namespace DashboardApi.Data.Models
{
    public class Integration
    {
        public int Id { get; set; }
        public string Type { get; set; } = "Telegram";
        public string ConfigJson { get; set; } = "{}";
        public byte[]? SessionData { get; set; }
        public int DashboardId { get; set; }
        [JsonIgnore] 
        public Dashboard Dashboard { get; set; } = null!;
    }
}
