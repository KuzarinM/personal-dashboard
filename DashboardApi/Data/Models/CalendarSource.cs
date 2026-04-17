using Newtonsoft.Json;

namespace DashboardApi.Data.Models
{
    public class CalendarSource
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Icon { get; set; } = "📅";
        public int DashboardId { get; set; }
        [JsonIgnore]
        public Dashboard Dashboard { get; set; } = null!;
    }
}
