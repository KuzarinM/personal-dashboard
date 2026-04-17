namespace DashboardApi.DTOs.Reminder
{
    public record ReminderDto(
        int Id,
        string Message,
        DateTime TargetTime,
        string RecurrenceType,
        int RecurrenceIntervalMin
    );
}
