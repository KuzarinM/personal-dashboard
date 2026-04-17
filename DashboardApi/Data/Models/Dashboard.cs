using Newtonsoft.Json;

namespace DashboardApi.Data.Models
{
    public class Dashboard
    {
        public int Id { get; set; }
        public string Title { get; set; } = "New Dashboard";
        public bool IsPublic { get; set; } = false; // Замена логики "default": если true, можно читать без токена
        public bool ScheduleEnabled { get; set; }
        public string ScheduleStart { get; set; } = "10:00";
        public string ScheduleEnd { get; set; } = "19:00";
        public string ScheduleDays { get; set; } = "1,2,3,4,5";
        public string WidgetLayout { get; set; } = "";
        public string HeaderLayout { get; set; } = "";
        public int UserId { get; set; }
        [JsonIgnore] 
        public User User { get; set; } = null!;
        public int UrgencyCriticalMin { get; set; } = 1440;
        public int UrgencyWarningMin { get; set; } = 10080;

        public List<Category> Categories { get; set; } = new();
        public List<CalendarSource> Calendars { get; set; } = new();
        public List<ManualEvent> ManualEvents { get; set; } = new();
        public List<Note> Notes { get; set; } = new();
        public List<Integration> Integrations { get; set; } = new();

        public List<Reminder> Reminders { get; set; } = new();
        public DashboardStatus? Status { get; set; }
    }
}
