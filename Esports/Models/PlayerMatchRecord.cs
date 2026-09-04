namespace Esports.Models
{
    public class PlayerMatchRecord
    {
        public int Id { get; set; }

        public string PlayerId { get; set; } = string.Empty;
        public ApplicationUser Player { get; set; } = null!;

        public int TeamId { get; set; }
        public Team Team { get; set; }

        public DateTime MatchDate { get; set; }
        public string? Opponent { get; set; }
        public string Result { get; set; } = string.Empty;   

        public string StatsJson { get; set; } = "{}";        

        public string? CoachNotes { get; set; }

        public string LoggedByUserId { get; set; } = string.Empty;
        public ApplicationUser LoggedByUser { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}