namespace Esports.Models
{
    public class DailySchedule
    {
        public int Id { get; set; }

        public int TeamId { get; set; }
        public Team Team { get; set; }

        public string CreatedByUserId { get; set; } = string.Empty;
        public ApplicationUser CreatedByUser { get; set; } = null!;

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        public DateTime ScheduleDate { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}