namespace DashboardApi.Data.Models
{
    public class ReportState
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Key { get; set; } = string.Empty; // e.g., "Dash_1_Links" или "Dash_1_Notes"
        public string Hash { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}
