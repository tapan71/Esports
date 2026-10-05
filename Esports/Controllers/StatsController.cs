using System.Security.Claims;
using Esports.Data;
using Esports.Models;
using Esports.Services;
using Esports.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Esports.Controllers
{
    [Authorize]
    public class StatsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStatsCalculatorService _statsCalculator;
        private readonly IPrizeCalculatorService _prizeCalculator;

        public StatsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IStatsCalculatorService statsCalculator,
            IPrizeCalculatorService prizeCalculator)
        {
            _context = context;
            _userManager = userManager;
            _statsCalculator = statsCalculator;
            _prizeCalculator = prizeCalculator;
        }

        private string? GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        // GET: /Stats/Player/guid-or-empty
        [HttpGet]
        public async Task<IActionResult> Player(string? id)
        {
            var currentUserId = GetCurrentUserId();
            if (string.IsNullOrEmpty(currentUserId)) return Challenge();

            var targetUserId = string.IsNullOrEmpty(id) ? currentUserId : id;

            // Authorization: Players can only view their own stats
            if (User.IsInRole("Player") && targetUserId != currentUserId)
            {
                return Forbid();
            }

            var player = await _userManager.FindByIdAsync(targetUserId);
            if (player == null) return NotFound();

            var currentTeam = await _context.TeamMemberships
                .Where(tm => tm.UserId == targetUserId && tm.LeftDate == null)
                .Include(tm => tm.Team)
                .Include(tm => tm.GameRole)
                .FirstOrDefaultAsync();

            var matchRecords = await _context.PlayerMatchRecords
                .Where(r => r.PlayerId == targetUserId)
                .Include(r => r.Team)
                .OrderByDescending(r => r.MatchDate)
                .AsNoTracking()
                .ToListAsync();

            int wins = 0, losses = 0, draws = 0;
            int totalKills = 0, totalDeaths = 0, totalAssists = 0;

            foreach (var match in matchRecords)
            {
                if (string.Equals(match.Result, "Win", StringComparison.OrdinalIgnoreCase)) wins++;
                else if (string.Equals(match.Result, "Loss", StringComparison.OrdinalIgnoreCase)) losses++;
                else draws++;

                var dto = _statsCalculator.ParseStatsJson(match.StatsJson);
                totalKills += dto.Kills;
                totalDeaths += dto.Deaths;
                totalAssists += dto.Assists;
            }

            var tournamentShares = await _context.TournamentParticipants
                .Where(tp => tp.PlayerId == targetUserId)
                .ToListAsync();

            var rewards = await _context.TournamentRewards
                .Where(tr => tr.PlayerId == targetUserId)
                .Include(tr => tr.Tournament)
                .OrderByDescending(tr => tr.Tournament.StartDate)
                .AsNoTracking()
                .ToListAsync();

            decimal totalBaseEarnings = tournamentShares.Sum(tp => tp.BaseShare);
            decimal totalBonusEarnings = rewards.Sum(r => r.BonusAmount);

            var viewModel = new PlayerStatsViewModel
            {
                Player = player,
                CurrentTeam = currentTeam,
                TotalMatches = matchRecords.Count,
                TotalWins = wins,
                TotalLosses = losses,
                TotalDraws = draws,
                TotalKills = totalKills,
                TotalDeaths = totalDeaths,
                TotalAssists = totalAssists,
                TotalBaseEarnings = totalBaseEarnings,
                TotalBonusEarnings = totalBonusEarnings,
                MatchHistory = matchRecords,
                RewardsReceived = rewards
            };

            return View(viewModel);
        }

        // GET: /Stats/Team/5
        [HttpGet]
        public async Task<IActionResult> Team(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var team = await _context.Teams
                .Include(t => t.Game)
                .Include(t => t.Owner)
                .Include(t => t.TeamStaff.Where(ts => ts.RemovedDate == null))
                    .ThenInclude(ts => ts.User)
                .Include(t => t.TeamMemberships.Where(tm => tm.LeftDate == null))
                    .ThenInclude(tm => tm.User)
                .Include(t => t.TeamMemberships.Where(tm => tm.LeftDate == null))
                    .ThenInclude(tm => tm.GameRole)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (team == null) return NotFound();

            bool isOwner = team.OwnerId == userId;
            bool isCoach = team.TeamStaff.Any(ts => ts.UserId == userId && ts.RemovedDate == null);
            bool isPlayer = team.TeamMemberships.Any(tm => tm.UserId == userId && tm.LeftDate == null);

            if (!isOwner && !isCoach && !isPlayer)
            {
                return Forbid();
            }

            var matches = await _context.PlayerMatchRecords
                .Where(r => r.TeamId == id)
                .Include(r => r.Player)
                .Include(r => r.Team)
                .OrderByDescending(r => r.MatchDate)
                .ThenByDescending(r => r.Id)
                .AsNoTracking()
                .ToListAsync();

            var playerRoles = team.TeamMemberships
                .ToDictionary(tm => tm.UserId, tm => tm.GameRole != null ? tm.GameRole.RoleName : "Player");

            var matchGroups = matches
                .GroupBy(r => new { Opponent = r.Opponent ?? "Scrim", Date = r.MatchDate.Date, r.Result })
                .Select(g =>
                {
                    var rawCoachNotes = g.FirstOrDefault(r => !string.IsNullOrEmpty(r.CoachNotes))?.CoachNotes;
                    if (!string.IsNullOrEmpty(rawCoachNotes) && rawCoachNotes.StartsWith("[Match MVP] "))
                    {
                        rawCoachNotes = rawCoachNotes.Substring("[Match MVP] ".Length).Trim();
                    }

                    var players = g.Select(r =>
                    {
                        var stats = _statsCalculator.ParseStatsJson(r.StatsJson);
                        var role = playerRoles.TryGetValue(r.PlayerId, out var rName) ? rName : "Player";
                        return new PlayerMatchRecordItemViewModel
                        {
                            RecordId = r.Id,
                            PlayerId = r.PlayerId,
                            PlayerName = r.Player?.FullName ?? "Player",
                            RoleName = role,
                            Kills = stats.Kills,
                            Deaths = stats.Deaths,
                            Assists = stats.Assists,
                            Score = stats.Score,
                            IsMvp = r.CoachNotes?.Contains("[Match MVP]") == true || r.CoachNotes?.Contains("[MVP]") == true
                        };
                    }).ToList();

                    return new TeamMatchGroupViewModel
                    {
                        TeamId = id,
                        TeamName = team.Name,
                        Opponent = g.Key.Opponent,
                        MatchDate = g.Key.Date,
                        Result = g.Key.Result,
                        CoachNotes = rawCoachNotes,
                        Players = players
                    };
                })
                .OrderByDescending(m => m.MatchDate)
                .ToList();

            int totalTeamMatches = matchGroups.Count;
            int wins = matchGroups.Count(m => string.Equals(m.Result, "Win", StringComparison.OrdinalIgnoreCase));
            int losses = matchGroups.Count(m => string.Equals(m.Result, "Loss", StringComparison.OrdinalIgnoreCase));
            int draws = totalTeamMatches - wins - losses;

            var tournaments = await _context.Tournaments
                .Where(t => t.TeamId == id)
                .OrderByDescending(t => t.StartDate)
                .AsNoTracking()
                .ToListAsync();

            decimal totalPrize = _prizeCalculator.CalculateTotalTeamPrizes(tournaments);

            var playerSummaries = new List<PlayerSummaryDto>();
            foreach (var membership in team.TeamMemberships)
            {
                var playerMatches = matches.Where(m => m.PlayerId == membership.UserId).ToList();
                var summary = _statsCalculator.AggregatePlayerStats(
                    membership.UserId,
                    membership.User.FullName,
                    membership.GameRole.RoleName,
                    playerMatches);

                playerSummaries.Add(summary);
            }

            var viewModel = new TeamStatsViewModel
            {
                Team = team,
                TotalMatches = totalTeamMatches,
                TotalWins = wins,
                TotalLosses = losses,
                TotalDraws = draws,
                TotalPrizeMoney = totalPrize,
                TournamentsCount = tournaments.Count,
                PlayerSummaries = playerSummaries,
                RecentMatches = matches.Take(10).ToList(),
                RecentMatchGroups = matchGroups.Take(10).ToList(),
                Tournaments = tournaments
            };

            return View(viewModel);
        }
    }
}
