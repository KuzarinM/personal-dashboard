using Newtonsoft.Json;

namespace DashboardApi.Data.Models
{
    public class ManualEvent
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Icon { get; set; } = "📌";
        public int DashboardId { get; set; }
        [JsonIgnore]
        public Dashboard Dashboard { get; set; } = null!;
    }
}
