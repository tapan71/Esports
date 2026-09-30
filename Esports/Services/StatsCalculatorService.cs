using System.Text.Json;
using Esports.Models;
using Esports.ViewModels;

namespace Esports.Services
{
    public class StatsCalculatorService : IStatsCalculatorService
    {
        public MatchStatsDto ParseStatsJson(string? statsJson)
        {
            if (string.IsNullOrWhiteSpace(statsJson))
            {
                return new MatchStatsDto();
            }

            try
            {
                return JsonSerializer.Deserialize<MatchStatsDto>(statsJson) ?? new MatchStatsDto();
            }
            catch
            {
                return new MatchStatsDto();
            }
        }

        public string SerializeStats(MatchStatsDto stats)
        {
            return JsonSerializer.Serialize(stats ?? new MatchStatsDto());
        }

        public double CalculateWinRate(int totalMatches, int wins)
        {
            if (totalMatches <= 0) return 0;
            return Math.Round(((double)wins / totalMatches) * 100, 1);
        }

        public double CalculateKda(int kills, int deaths, int assists)
        {
            if (deaths <= 0)
            {
                return kills + assists;
            }

            return Math.Round((double)(kills + assists) / deaths, 2);
        }

        public PlayerSummaryDto AggregatePlayerStats(string playerId, string playerName, string roleName, IEnumerable<PlayerMatchRecord> records)
        {
            int totalKills = 0;
            int totalDeaths = 0;
            int totalAssists = 0;
            int count = 0;

            if (records != null)
            {
                foreach (var record in records)
                {
                    count++;
                    var stats = ParseStatsJson(record.StatsJson);
                    totalKills += stats.Kills;
                    totalDeaths += stats.Deaths;
                    totalAssists += stats.Assists;
                }
            }

            return new PlayerSummaryDto
            {
                PlayerId = playerId,
                FullName = playerName,
                RoleName = roleName,
                MatchesCount = count,
                Kills = totalKills,
                Deaths = totalDeaths,
                Assists = totalAssists
            };
        }
    }
}
