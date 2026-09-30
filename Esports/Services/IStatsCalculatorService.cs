using Esports.Models;
using Esports.ViewModels;

namespace Esports.Services
{
    public interface IStatsCalculatorService
    {
        MatchStatsDto ParseStatsJson(string? statsJson);
        string SerializeStats(MatchStatsDto stats);
        double CalculateWinRate(int totalMatches, int wins);
        double CalculateKda(int kills, int deaths, int assists);
        PlayerSummaryDto AggregatePlayerStats(string playerId, string playerName, string roleName, IEnumerable<PlayerMatchRecord> records);
    }
}
