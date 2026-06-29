using Newtonsoft.Json;

namespace DashboardApi.Data.Models
{
    public class DndCatalog
    {
        public int Id { get; set; }
        public int UserId { get; set; }

        [JsonIgnore]
        public User User { get; set; } = null!;

        // Храним общий каталог предметов и заклинаний пользователя
        public string DataJson { get; set; } = "{\"items\":[],\"spells\":[]}";
    }
}
