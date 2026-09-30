using Esports.Models;

namespace Esports.Services
{
    public interface IPrizeCalculatorService
    {
        decimal CalculateBaseShare(decimal prizePoolWon, int participantCount);
        decimal CalculateTotalPlayerEarnings(IEnumerable<TournamentParticipant> participations, IEnumerable<TournamentReward> bonusRewards);
        decimal CalculateTotalTeamPrizes(IEnumerable<Tournament> tournaments);
    }
}
