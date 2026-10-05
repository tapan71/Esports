using System.Security.Claims;
using Esports.Data;
using Esports.Models;
using Esports.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using Esports.Services;

namespace Esports.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStatsCalculatorService _statsCalculator;

        public DashboardController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IStatsCalculatorService statsCalculator)
        {
            _context = context;
            _userManager = userManager;
            _statsCalculator = statsCalculator;
        }

        private string? GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        // GET: /Dashboard/Owner
        [HttpGet]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> Owner()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var user = await _userManager.FindByIdAsync(userId);
            var today = DateTime.UtcNow.Date;

            var teams = await _context.Teams
                .Where(t => t.OwnerId == userId)
                .Include(t => t.Game)
                .Include(t => t.TeamStaff.Where(ts => ts.RemovedDate == null))
                    .ThenInclude(ts => ts.User)
                .Include(t => t.TeamMemberships.Where(tm => tm.LeftDate == null))
                    .ThenInclude(tm => tm.User)
                .AsNoTracking()
                .ToListAsync();

            var teamIds = teams.Select(t => t.Id).ToList();

            int totalPlayers = teams.Sum(t => t.TeamMemberships.Count);
            decimal totalPrize = await _context.Tournaments
                .Where(t => teamIds.Contains(t.TeamId))
                .SumAsync(t => (decimal?)t.PrizePoolWon) ?? 0;

            int totalMatches = await _context.PlayerMatchRecords
                .CountAsync(r => teamIds.Contains(r.TeamId));

            var todaySchedules = await _context.DailySchedules
                .Where(ds => teamIds.Contains(ds.TeamId) && ds.ScheduleDate.Date == today)
                .Include(ds => ds.Team)
                .OrderBy(ds => ds.StartTime)
                .AsNoTracking()
                .ToListAsync();

            var recentTournaments = await _context.Tournaments
                .Where(t => teamIds.Contains(t.TeamId))
                .Include(t => t.Team)
                .OrderByDescending(t => t.StartDate)
                .Take(5)
                .AsNoTracking()
                .ToListAsync();

            var viewModel = new OwnerDashboardViewModel
            {
                OwnerName = user?.FullName ?? "Owner",
                Teams = teams,
                TotalActivePlayers = totalPlayers,
                TotalPrizeMoney = totalPrize,
                TotalMatchesLogged = totalMatches,
                TodaySchedules = todaySchedules,
                RecentTournaments = recentTournaments
            };

            return View(viewModel);
        }

        // GET: /Dashboard/Coach
        [HttpGet]
        [Authorize(Roles = "Coach")]
        public async Task<IActionResult> Coach()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var user = await _userManager.FindByIdAsync(userId);
            var today = DateTime.UtcNow.Date;

            // Find current active team where this user is coach
            var activeStaffRecord = await _context.TeamStaff
                .Include(ts => ts.Team)
                    .ThenInclude(t => t.Game)
                .FirstOrDefaultAsync(ts => ts.UserId == userId && ts.RemovedDate == null);

            var viewModel = new CoachDashboardViewModel
            {
                CoachName = user?.FullName ?? "Coach"
            };

            if (activeStaffRecord != null)
            {
                viewModel.AssignedTeam = activeStaffRecord.Team;

                viewModel.ActiveRoster = await _context.TeamMemberships
                    .Where(tm => tm.TeamId == activeStaffRecord.TeamId && tm.LeftDate == null)
                    .Include(tm => tm.User)
                    .Include(tm => tm.GameRole)
                    .AsNoTracking()
                    .ToListAsync();

                viewModel.TodaySchedules = await _context.DailySchedules
                    .Where(ds => ds.TeamId == activeStaffRecord.TeamId && ds.ScheduleDate.Date == today)
                    .OrderBy(ds => ds.StartTime)
                    .AsNoTracking()
                    .ToListAsync();

                var records = await _context.PlayerMatchRecords
                    .Where(r => r.TeamId == activeStaffRecord.TeamId)
                    .Include(r => r.Player)
                    .Include(r => r.Team)
                    .OrderByDescending(r => r.MatchDate)
                    .ThenByDescending(r => r.Id)
                    .AsNoTracking()
                    .ToListAsync();

                var playerRoles = await _context.TeamMemberships
                    .Where(tm => tm.TeamId == activeStaffRecord.TeamId && tm.LeftDate == null)
                    .Include(tm => tm.GameRole)
                    .ToDictionaryAsync(tm => tm.UserId, tm => tm.GameRole != null ? tm.GameRole.RoleName : "Player");

                viewModel.RecentMatches = records
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
                            TeamId = activeStaffRecord.TeamId,
                            TeamName = activeStaffRecord.Team?.Name ?? "Team",
                            Opponent = g.Key.Opponent,
                            MatchDate = g.Key.Date,
                            Result = g.Key.Result,
                            CoachNotes = rawCoachNotes,
                            Players = players
                        };
                    })
                    .OrderByDescending(m => m.MatchDate)
                    .Take(5)
                    .ToList();

                viewModel.RecentRecords = records.Take(5).ToList();
            }

            return View(viewModel);
        }

        // GET: /Dashboard/Player
        [HttpGet]
        [Authorize(Roles = "Player")]
        public async Task<IActionResult> Player()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var user = await _userManager.FindByIdAsync(userId);
            var today = DateTime.UtcNow.Date;

            // Find current active team membership
            var activeMembership = await _context.TeamMemberships
                .Include(tm => tm.Team)
                    .ThenInclude(t => t.Game)
                .Include(tm => tm.GameRole)
                .FirstOrDefaultAsync(tm => tm.UserId == userId && tm.LeftDate == null);

            var recentMatches = await _context.PlayerMatchRecords
                .Where(r => r.PlayerId == userId)
                .Include(r => r.Team)
                .OrderByDescending(r => r.MatchDate)
                .Take(5)
                .AsNoTracking()
                .ToListAsync();

            var tournamentShares = await _context.TournamentParticipants
                .Where(tp => tp.PlayerId == userId)
                .Include(tp => tp.Tournament)
                .OrderByDescending(tp => tp.Tournament.StartDate)
                .AsNoTracking()
                .ToListAsync();

            var specialRewards = await _context.TournamentRewards
                .Where(tr => tr.PlayerId == userId)
                .Include(tr => tr.Tournament)
                .OrderByDescending(tr => tr.Tournament.StartDate)
                .AsNoTracking()
                .ToListAsync();

            decimal totalBaseEarnings = tournamentShares.Sum(tp => tp.BaseShare);
            decimal totalBonusEarnings = specialRewards.Sum(tr => tr.BonusAmount);

            List<DailySchedule> todaySchedules = new();
            if (activeMembership != null)
            {
                todaySchedules = await _context.DailySchedules
                    .Where(ds => ds.TeamId == activeMembership.TeamId && ds.ScheduleDate.Date == today)
                    .OrderBy(ds => ds.StartTime)
                    .AsNoTracking()
                    .ToListAsync();
            }

            var viewModel = new PlayerDashboardViewModel
            {
                PlayerName = user?.FullName ?? "Player",
                CurrentTeamMembership = activeMembership,
                TodaySchedules = todaySchedules,
                RecentMatches = recentMatches,
                TotalEarnings = totalBaseEarnings + totalBonusEarnings,
                TournamentShares = tournamentShares,
                SpecialRewards = specialRewards
            };

            return View(viewModel);
        }
    }
}