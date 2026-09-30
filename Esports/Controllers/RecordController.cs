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

        // GET: /Record?teamId=5&playerId=abc
        [HttpGet]
        public async Task<IActionResult> Index(int? teamId, string? playerId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

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

            var records = await query.OrderByDescending(r => r.MatchDate).AsNoTracking().ToListAsync();

            var viewModel = new RecordIndexViewModel
            {
                TeamId = teamId,
                PlayerId = playerId,
                Records = records,
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
        public async Task<IActionResult> Create(int teamId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            if (!await CanUserManageTeamAsync(teamId, userId))
            {
                return Forbid();
            }

            var team = await _context.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId);
            if (team == null) return NotFound();

            var activePlayers = await _context.TeamMemberships
                .Where(tm => tm.TeamId == teamId && tm.LeftDate == null)
                .Include(tm => tm.User)
                .Include(tm => tm.GameRole)
                .Select(tm => new SelectListItem
                {
                    Value = tm.UserId,
                    Text = $"{tm.User.FullName} ({tm.GameRole.RoleName})"
                })
                .ToListAsync();

            var viewModel = new RecordCreateViewModel
            {
                TeamId = team.Id,
                TeamName = team.Name,
                MatchDate = DateTime.UtcNow.Date,
                AvailablePlayers = activePlayers
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

            bool isPlayerOnTeam = await _context.TeamMemberships
                .AnyAsync(tm => tm.TeamId == model.TeamId && tm.UserId == model.PlayerId && tm.LeftDate == null);

            if (!isPlayerOnTeam)
            {
                ModelState.AddModelError("PlayerId", "The player is not an active member of this team.");
            }

            if (!ModelState.IsValid)
            {
                var team = await _context.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == model.TeamId);
                model.TeamName = team?.Name ?? string.Empty;
                model.AvailablePlayers = await _context.TeamMemberships
                    .Where(tm => tm.TeamId == model.TeamId && tm.LeftDate == null)
                    .Include(tm => tm.User)
                    .Include(tm => tm.GameRole)
                    .Select(tm => new SelectListItem
                    {
                        Value = tm.UserId,
                        Text = $"{tm.User.FullName} ({tm.GameRole.RoleName})"
                    })
                    .ToListAsync();

                return View(model);
            }

            var statsObj = new MatchStatsDto
            {
                Kills = model.Kills,
                Deaths = model.Deaths,
                Assists = model.Assists,
                Score = model.Score
            };

            var record = new PlayerMatchRecord
            {
                TeamId = model.TeamId,
                PlayerId = model.PlayerId,
                MatchDate = model.MatchDate,
                Opponent = model.Opponent?.Trim(),
                Result = model.Result,
                StatsJson = _statsCalculator.SerializeStats(statsObj),
                CoachNotes = model.CoachNotes?.Trim(),
                LoggedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.PlayerMatchRecords.Add(record);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Player match performance recorded.";
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
    }
}
