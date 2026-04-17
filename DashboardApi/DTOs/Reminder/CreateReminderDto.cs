namespace DashboardApi.DTOs.Reminder
{
    public record CreateReminderDto(
        string Message,
        DateTime TargetTime,
        string RecurrenceType,
        int RecurrenceIntervalMin
    );
}
