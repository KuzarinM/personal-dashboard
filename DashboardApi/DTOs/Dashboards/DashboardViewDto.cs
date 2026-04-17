using DashboardApi.DTOs.Calendars;
using DashboardApi.DTOs.Category;
using DashboardApi.DTOs.Events;
using DashboardApi.DTOs.Nodes;
using DashboardApi.DTOs.Settings;

namespace DashboardApi.DTOs.Dashboard
{
    public record DashboardViewDto(
        int Id,               // Добавляем ID, чтобы фронт знал, куда слать апдейты
        string Title,
        bool IsPublic,
        ScheduleSettingsDto Schedule,
        List<CategoryViewDto> Categories,
        List<CalendarViewDto> Calendars,
        List<ManualEventViewDto> ManualEvents,
        List<NoteDto> Notes,  // Убеждаемся, что заметки тут есть
        List<string> ActiveIntegrations,
        UrgencySettingsDto Urgency,
        string WidgetLayout,
        string HeaderLayout,
        string MyRole,
        List<string> TeamMembers
    );

}
