using Newtonsoft.Json;

namespace DashboardApi.Data.Models
{
    public class Reminder
    {
        public int Id { get; set; }
        public string Message { get; set; } = "";
        public DateTime TargetTime { get; set; } // UTC

        public string RecurrenceType { get; set; } = "None"; // "None", "Daily", "Interval"
        public int RecurrenceIntervalMin { get; set; } = 0;  // Если Interval, то сколько минут

        public int DashboardId { get; set; }
        [JsonIgnore]
        public Dashboard Dashboard { get; set; } = null!;
    }
}
