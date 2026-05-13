using Newtonsoft.Json;

namespace DashboardApi.Data.Models
{
    public class Tag
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty; // Уникальное глобальное имя ("dev", "meeting")
        [JsonIgnore]
        public List<TaskItem> Tasks { get; set; } = new();
    }
}
