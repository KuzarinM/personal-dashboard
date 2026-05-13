using Newtonsoft.Json;

namespace DashboardApi.Data.Models
{
    public class TimeEntry
    {
        public int Id { get; set; }
        public int TaskItemId { get; set; }
        [JsonIgnore]
        public TaskItem TaskItem { get; set; } = null!;

        public DateTime Date { get; set; } // Дата за которую списали
        public int DurationMinutes { get; set; } // Храним в минутах - идеально для SQL SUM()
    }
}
