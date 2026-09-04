namespace Esports.Models
{
    public class TeamStaff
    {
        public int Id { get; set; }

        public int TeamId { get; set; }
        public Team Team { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public DateTime HiredDate { get; set; } = DateTime.UtcNow;
        public DateTime? RemovedDate { get; set; }   // null = currently active staff
    }
}
