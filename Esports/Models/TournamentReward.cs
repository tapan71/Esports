namespace Esports.Models
{
    public class TournamentReward
    {
        public int Id { get; set; }

        public int TournamentId { get; set; }
        public Tournament Tournament { get; set; } = null!;

        public string PlayerId { get; set; } = string.Empty;
        public ApplicationUser Player { get; set; } = null!;

        public string RewardTitle { get; set; } = string.Empty;   // e.g. "MVP", "Best Fragger"
        public decimal BonusAmount { get; set; }
    }
}