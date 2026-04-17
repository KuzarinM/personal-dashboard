using Newtonsoft.Json;

namespace DashboardApi.Data.Models
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        [JsonIgnore]
        public string PasswordHash { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }

        // --- STATUS FIELDS ---
        public string? StatusText { get; set; } // "Away", "Coding", "Lunch"
        public string? StatusEmoji { get; set; } // "🍔", "💻"
        public string? StatusMessage { get; set; } // "Вернусь через час, ушел за едой"
        public string? StatusColor { get; set; } // "emerald", "amber", "red" (Tailwind names or hex)
        public DateTime? LastStatusUpdate { get; set; }

        public List<Dashboard> Dashboards { get; set; } = new();
    }
}
