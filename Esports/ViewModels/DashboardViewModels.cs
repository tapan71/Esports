using Esports.Models;

namespace Esports.ViewModels
{
    public class OwnerDashboardViewModel
    {
        public string OwnerName { get; set; } = string.Empty;
        public List<Team> Teams { get; set; } = new();
        public int TotalActivePlayers { get; set; }
        public decimal TotalPrizeMoney { get; set; }
        public int TotalMatchesLogged { get; set; }
        public List<DailySchedule> TodaySchedules { get; set; } = new();
        public List<Tournament> RecentTournaments { get; set; } = new();
    }

    public class CoachDashboardViewModel
    {
        public string CoachName { get; set; } = string.Empty;
        public Team? AssignedTeam { get; set; }
        public List<TeamMembership> ActiveRoster { get; set; } = new();
        public List<DailySchedule> TodaySchedules { get; set; } = new();
        public List<PlayerMatchRecord> RecentRecords { get; set; } = new();
    }

    public class PlayerDashboardViewModel
    {
        public string PlayerName { get; set; } = string.Empty;
        public TeamMembership? CurrentTeamMembership { get; set; }
        public List<DailySchedule> TodaySchedules { get; set; } = new();
        public List<PlayerMatchRecord> RecentMatches { get; set; } = new();
        public decimal TotalEarnings { get; set; }
        public List<TournamentParticipant> TournamentShares { get; set; } = new();
        public List<TournamentReward> SpecialRewards { get; set; } = new();
    }
}
