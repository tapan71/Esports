namespace Esports.Models
{
    public class TeamMembership
    {
        public int Id { get; set; }

        public int TeamId { get; set; }
        public Team Team { get; set; }

        public string UserId { get; set; }
        public ApplicationUser User { get; set; }

        public int GameRoleId { get; set; }
        public GameRole GameRole { get; set; }

        public DateTime JoinedDate { get; set; } = DateTime.UtcNow;
        public DateTime? LeftDate { get; set; }   // null = currently active on the team
    }
}
