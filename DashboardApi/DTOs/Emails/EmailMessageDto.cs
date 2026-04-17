namespace DashboardApi.DTOs.Email
{
    public record EmailMessageDto(
        string Id,
        string From,
        string Subject,
        DateTime Date,
        bool IsNew // Всегда true, раз мы ищем непрочитанные
    );
}
