namespace Esports.Models
{
    public class Tournament
    {
        public int Id { get; set; }

        public int TeamId { get; set; }
        public Team Team { get; set; }

        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Placement { get; set; }         
        public decimal PrizePoolWon { get; set; }
        public string? Notes { get; set; }

        public string LoggedByUserId { get; set; } = string.Empty;
        public ApplicationUser LoggedByUser { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public ICollection<TournamentParticipant> Participants { get; set; } = new List<TournamentParticipant>();
        public ICollection<TournamentReward> Rewards { get; set; } = new List<TournamentReward>();
    }
}