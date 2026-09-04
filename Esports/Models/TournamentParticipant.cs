namespace Esports.Models
{
    public class TournamentParticipant
    {
        public int Id { get; set; }

        public int TournamentId { get; set; }
        public Tournament Tournament { get; set; } = null!;

        public string PlayerId { get; set; } = string.Empty;
        public ApplicationUser Player { get; set; } = null!;

        public decimal BaseShare { get; set; }   // PrizePoolWon / ParticipantCount, calculated at save time
    }
}