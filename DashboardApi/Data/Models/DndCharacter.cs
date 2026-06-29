using Newtonsoft.Json;

namespace DashboardApi.Data.Models
{
    public class DndCharacter
    {
        public int Id { get; set; }
        public int DashboardId { get; set; }

        [JsonIgnore]
        public Dashboard Dashboard { get; set; } = null!;

        // Храним весь лист персонажа в виде сырого JSON
        public string DataJson { get; set; } = "{}";
    }
}
