using Newtonsoft.Json;

namespace DashboardApi.Data.Models
{
    public class Item
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Url { get; set; }
        public string? UrlLocal { get; set; }
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public int CategoryId { get; set; }
        [JsonIgnore] 
        public Category Category { get; set; } = null!;
    }
}
