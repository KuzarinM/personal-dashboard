namespace DashboardApi.DTOs.Email
{
    public record EmailConfigDto(
        string Host,       // "imap.gmail.com"
        int Port,          // 993
        bool UseSsl,       // true
        string Username,   // "me@gmail.com"
        string Password    // "app-password"
    );
}
