namespace DashboardApi.DTOs.Users
{
    public record UpdateUserStatusDto(
            string StatusText,
            string StatusEmoji,
            string StatusMessage,
            string StatusColor
        );
}
