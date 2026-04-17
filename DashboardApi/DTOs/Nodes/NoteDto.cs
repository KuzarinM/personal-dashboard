namespace DashboardApi.DTOs.Nodes
{
    public record NoteDto(
        int? Id,
        string Title,
        string Content,
        bool IsArchived = false,
        bool IsPinned = false,
        string Type = "Text",
        Guid? PublicId = null
    );
}
