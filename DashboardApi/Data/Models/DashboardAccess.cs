using Newtonsoft.Json;

namespace DashboardApi.Data.Models
{
    public class DashboardAccess
    {
        public int Id { get; set; }
        public int DashboardId { get; set; }
        [JsonIgnore]
        public Dashboard Dashboard { get; set; } = null!;

        public int UserId { get; set; }
        [JsonIgnore]
        public User User { get; set; } = null!;

        // "Editor" - может менять контент (заметки, галочки)
        // "Viewer" - только смотреть (если дашборд приватный)
        public string Role { get; set; } = "Editor";
    }
}
