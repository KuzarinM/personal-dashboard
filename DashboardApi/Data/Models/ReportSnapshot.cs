namespace DashboardApi.Data.Models
{
    public class ReportSnapshot
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; }

        // Сериализованное состояние активных заметок на момент создания снимка
        public string NotesJson { get; set; } = "[]";

        // Сериализованное состояние структуры категорий и ссылок на момент создания снимка
        public string LinksJson { get; set; } = "[]";
    }
}
