using Microsoft.Extensions.Logging;

namespace Esports.Services
{
    public class EmailService : IEmailService
    {
        private readonly ILogger<EmailService> _logger;

        public EmailService(ILogger<EmailService> logger)
        {
            _logger = logger;
        }

        public Task SendEmailAsync(string toEmail, string subject, string message)
        {
            _logger.LogInformation("Sending email to {ToEmail} with Subject: {Subject}. Content: {Message}", toEmail, subject, message);
            return Task.CompletedTask;
        }

        public Task SendWelcomeEmailAsync(string toEmail, string userName, string role)
        {
            string message = $"Hello {userName},\n\nWelcome to EsportsManager! Your account has been registered with the role: {role}.";
            return SendEmailAsync(toEmail, "Welcome to EsportsManager", message);
        }

        public Task SendScheduleNotificationAsync(string toEmail, string teamName, string title, DateTime date, TimeSpan startTime)
        {
            string message = $"Team {teamName} has scheduled '{title}' on {date:yyyy-MM-dd} at {startTime:hh\\:mm}.";
            return SendEmailAsync(toEmail, $"New Team Schedule: {title}", message);
        }
    }
}
