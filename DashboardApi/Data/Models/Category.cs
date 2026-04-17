using Newtonsoft.Json;

namespace DashboardApi.Data.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public int Order { get; set; }
        public int DashboardId { get; set; }
        [JsonIgnore] 
        public Dashboard Dashboard { get; set; } = null!;
        public List<Item> Items { get; set; } = new();
    }
}
