using Newtonsoft.Json;

namespace DashboardApi.Data.Models
{
    public class Note
    {
        public int Id { get; set; }
        public string Title { get; set; } = "Note";
        public string Content { get; set; } = "";
        public bool IsArchived { get; set; }
        public bool IsPinned { get; set; }
        public int DashboardId { get; set; }
        public string Type { get; set; } = "Text";
        public Guid? PublicId { get; set; }
        [JsonIgnore] 
        public Dashboard Dashboard { get; set; } = null!;
    }
}
