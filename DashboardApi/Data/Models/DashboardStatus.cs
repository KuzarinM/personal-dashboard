using Newtonsoft.Json;

namespace DashboardApi.Data.Models
{
    public class DashboardStatus
    {
        public int Id { get; set; }
        public bool IsBreak { get; set; }
        public long BreakStartTs { get; set; }
        public long TotalBreakMs { get; set; }
        public DateTime LastUpdate { get; set; }
        public int DashboardId { get; set; }
        [JsonIgnore]
        public Dashboard Dashboard { get; set; } = null!;
    }
}
