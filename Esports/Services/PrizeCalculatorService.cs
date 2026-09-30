using Esports.Models;

namespace Esports.Services
{
    public class PrizeCalculatorService : IPrizeCalculatorService
    {
        public decimal CalculateBaseShare(decimal prizePoolWon, int participantCount)
        {
            if (prizePoolWon <= 0 || participantCount <= 0)
            {
                return 0m;
            }

            return Math.Round(prizePoolWon / participantCount, 2);
        }

        public decimal CalculateTotalPlayerEarnings(IEnumerable<TournamentParticipant> participations, IEnumerable<TournamentReward> bonusRewards)
        {
            decimal baseTotal = participations?.Sum(p => p.BaseShare) ?? 0m;
            decimal bonusTotal = bonusRewards?.Sum(r => r.BonusAmount) ?? 0m;
            return baseTotal + bonusTotal;
        }

        public decimal CalculateTotalTeamPrizes(IEnumerable<Tournament> tournaments)
        {
            return tournaments?.Sum(t => t.PrizePoolWon) ?? 0m;
        }
    }
}
