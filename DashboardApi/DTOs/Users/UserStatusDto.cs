namespace DashboardApi.DTOs.Users
{
    public record UserStatusDto(
            string Username,
            string StatusText,
            string StatusEmoji,
            string StatusMessage,
            string StatusColor,
            DateTime? UpdatedAt
        );
}
