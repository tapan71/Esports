namespace Esports.Models
{
    public class Team
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string LogoUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int GameId { get; set; }
        public Game Game { get; set; }

        public string OwnerId { get; set; }
        public ApplicationUser Owner { get; set; }

        // Navigation properties
        public ICollection<TeamStaff> TeamStaff { get; set; }
        public ICollection<TeamMembership> TeamMemberships { get; set; }
    }
}