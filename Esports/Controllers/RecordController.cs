using System.Security.Claims;
using Esports.Data;
using Esports.Models;
using Esports.Services;
using Esports.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Esports.Controllers
{
    [Authorize]
    public class RecordController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStatsCalculatorService _statsCalculator;

        public RecordController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IStatsCalculatorService statsCalculator)
        {
            _context = context;
            _userManager = userManager;
            _statsCalculator = statsCalculator;
        }

        private string? GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private async Task<bool> CanUserManageTeamAsync(int teamId, string userId)
        {
            if (User.IsInRole("Owner"))
            {
                return await _context.Teams.AnyAsync(t => t.Id == teamId && t.OwnerId == userId);
            }

            if (User.IsInRole("Coach"))
            {
                return await _context.TeamStaff.AnyAsync(ts => ts.TeamId == teamId && ts.UserId == userId && ts.RemovedDate == null);
            }

            return false;
        }

        private async Task<int?> ResolveDefaultTeamIdAsync(string userId)
        {
            if (User.IsInRole("Coach"))
            {
                var coachTeam = await _context.TeamStaff
                    .Where(ts => ts.UserId == userId && ts.RemovedDate == null)
                    .Select(ts => (int?)ts.TeamId)
                    .FirstOrDefaultAsync();
                if (coachTeam.HasValue) return coachTeam.Value;
            }

            if (User.IsInRole("Owner"))
            {
                var ownedTeam = await _context.Teams
                    .Where(t => t.OwnerId == userId)
                    .Select(t => (int?)t.Id)
                    .FirstOrDefaultAsync();
                if (ownedTeam.HasValue) return ownedTeam.Value;
            }

            if (User.IsInRole("Player"))
            {
                var playerTeam = await _context.TeamMemberships
                    .Where(tm => tm.UserId == userId && tm.LeftDate == null)
                    .Select(tm => (int?)tm.TeamId)
                    .FirstOrDefaultAsync();
                if (playerTeam.HasValue) return playerTeam.Value;
            }

            return null;
        }

        private static int GetRoleOrder(string roleName) => roleName.ToLower() switch
        {
            "top" => 1,
            "jungle" => 2,
            "mid" => 3,
            "adc" or "bot" or "bottom" => 4,
            "support" or "sup" => 5,
            _ => 6
        };

        // GET: /Record?teamId=5&playerId=abc
        [HttpGet]
        public async Task<IActionResult> Index(int? teamId, string? playerId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            if (!teamId.HasValue && !User.IsInRole("Player"))
            {
                teamId = await ResolveDefaultTeamIdAsync(userId);
            }

            IQueryable<PlayerMatchRecord> query = _context.PlayerMatchRecords
                .Include(r => r.Team)
                .Include(r => r.Player)
                .Include(r => r.LoggedByUser);

            if (User.IsInRole("Player"))
            {
                // Players have read-only access to their own records
                query = query.Where(r => r.PlayerId == userId);
            }
            else if (User.IsInRole("Coach"))
            {
                var coachedTeamIds = await _context.TeamStaff
                    .Where(ts => ts.UserId == userId && ts.RemovedDate == null)
                    .Select(ts => ts.TeamId)
                    .ToListAsync();

                query = query.Where(r => coachedTeamIds.Contains(r.TeamId));
            }
            else if (User.IsInRole("Owner"))
            {
                var ownedTeamIds = await _context.Teams
                    .Where(t => t.OwnerId == userId)
                    .Select(t => t.Id)
                    .ToListAsync();

                query = query.Where(r => ownedTeamIds.Contains(r.TeamId));
            }

            if (teamId.HasValue)
            {
                query = query.Where(r => r.TeamId == teamId.Value);
            }

            if (!string.IsNullOrEmpty(playerId))
            {
                query = query.Where(r => r.PlayerId == playerId);
            }

            var records = await query.OrderByDescending(r => r.MatchDate).ThenBy(r => r.Id).AsNoTracking().ToListAsync();

            string? teamName = null;
            if (teamId.HasValue)
            {
                teamName = await _context.Teams.Where(t => t.Id == teamId.Value).Select(t => t.Name).FirstOrDefaultAsync();
            }

            var playerRoles = await _context.TeamMemberships
                .Where(tm => tm.LeftDate == null)
                .Include(tm => tm.GameRole)
                .ToDictionaryAsync(tm => $"{tm.TeamId}_{tm.UserId}", tm => tm.GameRole != null ? tm.GameRole.RoleName : "Player");

            var teamMatches = records
                .GroupBy(r => new { r.TeamId, Opponent = r.Opponent ?? "Scrim", Date = r.MatchDate.Date, r.Result })
                .Select(g =>
                {
                    var first = g.First();
                    var rawCoachNotes = g.FirstOrDefault(r => !string.IsNullOrEmpty(r.CoachNotes))?.CoachNotes;
                    if (!string.IsNullOrEmpty(rawCoachNotes) && rawCoachNotes.StartsWith("[Match MVP] "))
                    {
                        rawCoachNotes = rawCoachNotes.Substring("[Match MVP] ".Length).Trim();
                    }

                    return new TeamMatchGroupViewModel
                    {
                        TeamId = g.Key.TeamId,
                        TeamName = first.Team?.Name ?? "Team",
                        Opponent = g.Key.Opponent,
                        MatchDate = g.Key.Date,
                        Result = g.Key.Result,
                        CoachNotes = rawCoachNotes,
                        LoggedByName = first.LoggedByUser?.FullName,
                        Players = g.Select(r =>
                        {
                            var stats = _statsCalculator.ParseStatsJson(r.StatsJson);
                            var roleKey = $"{r.TeamId}_{r.PlayerId}";
                            var role = playerRoles.TryGetValue(roleKey, out var rName) ? rName : "Player";
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
                        }).OrderBy(p => GetRoleOrder(p.RoleName)).ToList()
                    };
                })
                .OrderByDescending(m => m.MatchDate)
                .ToList();

            var viewModel = new RecordIndexViewModel
            {
                TeamId = teamId,
                TeamName = teamName,
                PlayerId = playerId,
                Records = records,
                TeamMatches = teamMatches,
                CanManage = teamId.HasValue && await CanUserManageTeamAsync(teamId.Value, userId)
            };

            return View(viewModel);
        }

        // GET: /Record/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var record = await _context.PlayerMatchRecords
                .Include(r => r.Team)
                .Include(r => r.Player)
                .Include(r => r.LoggedByUser)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record == null) return NotFound();

            if (User.IsInRole("Player") && record.PlayerId != userId)
            {
                return Forbid();
            }

            if (!User.IsInRole("Player") && !await CanUserManageTeamAsync(record.TeamId, userId))
            {
                return Forbid();
            }

            var statsDto = _statsCalculator.ParseStatsJson(record.StatsJson);

            var viewModel = new RecordDetailsViewModel
            {
                Record = record,
                Stats = statsDto,
                CanManage = await CanUserManageTeamAsync(record.TeamId, userId)
            };

            return View(viewModel);
        }

        // GET: /Record/Create?teamId=5
        [HttpGet]
        public async Task<IActionResult> Create(int? teamId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            if (!teamId.HasValue || teamId.Value <= 0)
            {
                var resolved = await ResolveDefaultTeamIdAsync(userId);
                if (!resolved.HasValue)
                {
                    TempData["ErrorMessage"] = "You must be assigned to or own a team to log match records.";
                    return RedirectToAction(nameof(Index));
                }
                teamId = resolved.Value;
            }

            if (!await CanUserManageTeamAsync(teamId.Value, userId))
            {
                return Forbid();
            }

            var team = await _context.Teams
                .Include(t => t.Game)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == teamId.Value);

            if (team == null) return NotFound();

            var activeMembershipsList = await _context.TeamMemberships
                .Where(tm => tm.TeamId == teamId.Value && tm.LeftDate == null)
                .Include(tm => tm.User)
                .Include(tm => tm.GameRole)
                .AsNoTracking()
                .ToListAsync();

            var activeMemberships = activeMembershipsList
                .OrderBy(tm => GetRoleOrder(tm.GameRole?.RoleName ?? ""))
                .ToList();

            var playerStats = activeMemberships.Select(m => new PlayerMatchStatInputModel
            {
                PlayerId = m.UserId,
                PlayerName = m.User.FullName,
                RoleName = m.GameRole?.RoleName ?? "Player",
                Kills = 0,
                Deaths = 0,
                Assists = 0,
                Score = 0,
                IsMvp = false
            }).ToList();

            var viewModel = new RecordCreateViewModel
            {
                TeamId = team.Id,
                TeamName = team.Name,
                GameName = team.Game?.Name ?? "Esports",
                MatchDate = DateTime.UtcNow.Date,
                PlayerStats = playerStats
            };

            return View(viewModel);
        }

        // POST: /Record/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RecordCreateViewModel model)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            if (!await CanUserManageTeamAsync(model.TeamId, userId))
            {
                return Forbid();
            }

            if (model.PlayerStats == null || !model.PlayerStats.Any())
            {
                ModelState.AddModelError("", "No players are in the team roster to record stats for. Please add players to the roster first.");
            }

            if (!ModelState.IsValid)
            {
                var team = await _context.Teams.Include(t => t.Game).AsNoTracking().FirstOrDefaultAsync(t => t.Id == model.TeamId);
                model.TeamName = team?.Name ?? string.Empty;
                model.GameName = team?.Game?.Name ?? "Esports";
                return View(model);
            }

            var opponentName = string.IsNullOrWhiteSpace(model.Opponent) ? "Practice / Scrim" : model.Opponent.Trim();

            // Record match performance for each player on the team in this single match
            foreach (var p in model.PlayerStats!)
            {
                var statsObj = new MatchStatsDto
                {
                    Kills = p.Kills,
                    Deaths = p.Deaths,
                    Assists = p.Assists,
                    Score = p.Score
                };

                var coachNoteWithMvp = (p.IsMvp ? "[Match MVP] " : "") + (model.CoachNotes?.Trim() ?? string.Empty);

                var record = new PlayerMatchRecord
                {
                    TeamId = model.TeamId,
                    PlayerId = p.PlayerId,
                    MatchDate = model.MatchDate.Date,
                    Opponent = opponentName,
                    Result = model.Result,
                    StatsJson = _statsCalculator.SerializeStats(statsObj),
                    CoachNotes = string.IsNullOrWhiteSpace(coachNoteWithMvp) ? null : coachNoteWithMvp,
                    LoggedByUserId = userId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.PlayerMatchRecords.Add(record);
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Match against '{opponentName}' logged successfully for all {model.PlayerStats.Count} team players!";
            return RedirectToAction(nameof(Index), new { teamId = model.TeamId });
        }

        // GET: /Record/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var record = await _context.PlayerMatchRecords
                .Include(r => r.Team)
                .Include(r => r.Player)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record == null) return NotFound();

            if (!await CanUserManageTeamAsync(record.TeamId, userId))
            {
                return Forbid();
            }

            var statsDto = _statsCalculator.ParseStatsJson(record.StatsJson);

            var viewModel = new RecordEditViewModel
            {
                Id = record.Id,
                TeamId = record.TeamId,
                TeamName = record.Team.Name,
                PlayerId = record.PlayerId,
                PlayerName = record.Player.FullName,
                MatchDate = record.MatchDate,
                Opponent = record.Opponent,
                Result = record.Result,
                Kills = statsDto.Kills,
                Deaths = statsDto.Deaths,
                Assists = statsDto.Assists,
                Score = statsDto.Score,
                CoachNotes = record.CoachNotes
            };

            return View(viewModel);
        }

        // POST: /Record/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, RecordEditViewModel model)
        {
            if (id != model.Id) return BadRequest();

            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var record = await _context.PlayerMatchRecords
                .Include(r => r.Team)
                .Include(r => r.Player)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record == null) return NotFound();

            if (!await CanUserManageTeamAsync(record.TeamId, userId))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                model.TeamName = record.Team.Name;
                model.PlayerName = record.Player.FullName;
                return View(model);
            }

            var statsObj = new MatchStatsDto
            {
                Kills = model.Kills,
                Deaths = model.Deaths,
                Assists = model.Assists,
                Score = model.Score
            };

            record.MatchDate = model.MatchDate;
            record.Opponent = model.Opponent?.Trim();
            record.Result = model.Result;
            record.StatsJson = _statsCalculator.SerializeStats(statsObj);
            record.CoachNotes = model.CoachNotes?.Trim();

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Match record updated successfully.";
            return RedirectToAction(nameof(Details), new { id = record.Id });
        }

        // POST: /Record/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var record = await _context.PlayerMatchRecords.FirstOrDefaultAsync(r => r.Id == id);
            if (record == null) return NotFound();

            if (!await CanUserManageTeamAsync(record.TeamId, userId))
            {
                return Forbid();
            }

            int teamId = record.TeamId;
            _context.PlayerMatchRecords.Remove(record);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Match record deleted.";
            return RedirectToAction(nameof(Index), new { teamId });
        }

        // POST: /Record/DeleteMatch
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteMatch(int teamId, string opponent, DateTime matchDate)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            if (!await CanUserManageTeamAsync(teamId, userId))
            {
                return Forbid();
            }

            var records = await _context.PlayerMatchRecords
                .Where(r => r.TeamId == teamId && r.Opponent == opponent && r.MatchDate.Date == matchDate.Date)
                .ToListAsync();

            if (records.Any())
            {
                _context.PlayerMatchRecords.RemoveRange(records);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Team match vs '{opponent}' deleted successfully.";
            }

            return RedirectToAction(nameof(Index), new { teamId });
        }
    }
}
