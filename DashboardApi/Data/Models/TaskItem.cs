using Newtonsoft.Json;

namespace DashboardApi.Data.Models
{
    public class TaskItem
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        [JsonIgnore]
        public User User { get; set; } = null!;
        public string Name { get; set; } = string.Empty; // Уникальное в рамках юзера
        public List<Tag> Tags { get; set; } = new();
        public List<TimeEntry> TimeEntries { get; set; } = new();
    }
}
