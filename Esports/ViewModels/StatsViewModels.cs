using Esports.Models;

namespace Esports.ViewModels
{
    public class PlayerStatsViewModel
    {
        public ApplicationUser Player { get; set; } = null!;
        public TeamMembership? CurrentTeam { get; set; }
        public int TotalMatches { get; set; }
        public int TotalWins { get; set; }
        public int TotalLosses { get; set; }
        public int TotalDraws { get; set; }
        public double WinRate => TotalMatches == 0 ? 0 : Math.Round(((double)TotalWins / TotalMatches) * 100, 1);
        public int TotalKills { get; set; }
        public int TotalDeaths { get; set; }
        public int TotalAssists { get; set; }
        public double OverallKda => TotalDeaths == 0 ? (TotalKills + TotalAssists) : Math.Round((double)(TotalKills + TotalAssists) / TotalDeaths, 2);
        public decimal TotalBaseEarnings { get; set; }
        public decimal TotalBonusEarnings { get; set; }
        public decimal GrandTotalEarnings => TotalBaseEarnings + TotalBonusEarnings;
        public List<PlayerMatchRecord> MatchHistory { get; set; } = new();
        public List<TournamentReward> RewardsReceived { get; set; } = new();
    }

    public class PlayerSummaryDto
    {
        public string PlayerId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public int MatchesCount { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
        public double KdaRatio => Deaths == 0 ? (Kills + Assists) : Math.Round((double)(Kills + Assists) / Deaths, 2);
    }

    public class TeamStatsViewModel
    {
        public Team Team { get; set; } = null!;
        public int TotalMatches { get; set; }
        public int TotalWins { get; set; }
        public int TotalLosses { get; set; }
        public int TotalDraws { get; set; }
        public double WinRate => TotalMatches == 0 ? 0 : Math.Round(((double)TotalWins / TotalMatches) * 100, 1);
        public decimal TotalPrizeMoney { get; set; }
        public int TournamentsCount { get; set; }
        public List<PlayerSummaryDto> PlayerSummaries { get; set; } = new();
        public List<PlayerMatchRecord> RecentMatches { get; set; } = new();
        public List<Tournament> Tournaments { get; set; } = new();
    }
}
