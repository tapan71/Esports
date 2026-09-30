namespace Esports.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string subject, string message);
        Task SendWelcomeEmailAsync(string toEmail, string userName, string role);
        Task SendScheduleNotificationAsync(string toEmail, string teamName, string title, DateTime date, TimeSpan startTime);
    }
}
