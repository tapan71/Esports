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
    public class TournamentController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPrizeCalculatorService _prizeCalculator;

        public TournamentController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IPrizeCalculatorService prizeCalculator)
        {
            _context = context;
            _userManager = userManager;
            _prizeCalculator = prizeCalculator;
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

        // GET: /Tournament?teamId=5
        [HttpGet]
        public async Task<IActionResult> Index(int? teamId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            IQueryable<Tournament> query = _context.Tournaments
                .Include(t => t.Team)
                .Include(t => t.Participants)
                .Include(t => t.Rewards);

            if (User.IsInRole("Player"))
            {
                query = query.Where(t => t.Participants.Any(p => p.PlayerId == userId) ||
                                         t.Team.TeamMemberships.Any(tm => tm.UserId == userId && tm.LeftDate == null));
            }
            else if (User.IsInRole("Coach"))
            {
                var coachedTeamIds = await _context.TeamStaff
                    .Where(ts => ts.UserId == userId && ts.RemovedDate == null)
                    .Select(ts => ts.TeamId)
                    .ToListAsync();

                query = query.Where(t => coachedTeamIds.Contains(t.TeamId));
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
                query = query.Where(t => t.TeamId == teamId.Value);
            }

            var tournaments = await query.OrderByDescending(t => t.StartDate).AsNoTracking().ToListAsync();

            var viewModel = new TournamentIndexViewModel
            {
                TeamId = teamId,
                Tournaments = tournaments,
                CanManage = teamId.HasValue && await CanUserManageTeamAsync(teamId.Value, userId)
            };

            return View(viewModel);
        }

        // GET: /Tournament/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var tournament = await _context.Tournaments
                .Include(t => t.Team)
                .Include(t => t.LoggedByUser)
                .Include(t => t.Participants)
                    .ThenInclude(p => p.Player)
                .Include(t => t.Rewards)
                    .ThenInclude(r => r.Player)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tournament == null) return NotFound();

            decimal totalDistributed = _prizeCalculator.CalculateTotalPlayerEarnings(tournament.Participants, tournament.Rewards);

            var viewModel = new TournamentDetailsViewModel
            {
                Tournament = tournament,
                TotalRewardsDistributed = totalDistributed,
                CanManage = await CanUserManageTeamAsync(tournament.TeamId, userId)
            };

            return View(viewModel);
        }

        // GET: /Tournament/Create?teamId=5
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

            var viewModel = new TournamentCreateViewModel
            {
                TeamId = team.Id,
                TeamName = team.Name,
                StartDate = DateTime.UtcNow.Date,
                EndDate = DateTime.UtcNow.Date,
                AvailablePlayers = activePlayers
            };

            return View(viewModel);
        }

        // POST: /Tournament/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TournamentCreateViewModel model)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            if (!await CanUserManageTeamAsync(model.TeamId, userId))
            {
                return Forbid();
            }

            if (model.EndDate < model.StartDate)
            {
                ModelState.AddModelError("EndDate", "End date cannot be earlier than start date.");
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

            var tournament = new Tournament
            {
                TeamId = model.TeamId,
                Name = model.Name.Trim(),
                StartDate = model.StartDate,
                EndDate = model.EndDate,
                Placement = model.Placement?.Trim(),
                PrizePoolWon = model.PrizePoolWon,
                Notes = model.Notes?.Trim(),
                LoggedByUserId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Tournaments.Add(tournament);
            await _context.SaveChangesAsync();

            // Calculate base share per participant using PrizeCalculatorService
            if (model.SelectedPlayerIds != null && model.SelectedPlayerIds.Count > 0)
            {
                decimal baseSharePerPlayer = _prizeCalculator.CalculateBaseShare(model.PrizePoolWon, model.SelectedPlayerIds.Count);

                foreach (var playerId in model.SelectedPlayerIds)
                {
                    _context.TournamentParticipants.Add(new TournamentParticipant
                    {
                        TournamentId = tournament.Id,
                        PlayerId = playerId,
                        BaseShare = baseSharePerPlayer
                    });
                }

                await _context.SaveChangesAsync();
            }

            TempData["SuccessMessage"] = $"Tournament '{tournament.Name}' added successfully.";
            return RedirectToAction(nameof(Details), new { id = tournament.Id });
        }

        // GET: /Tournament/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var tournament = await _context.Tournaments
                .Include(t => t.Team)
                .Include(t => t.Participants)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tournament == null) return NotFound();

            if (!await CanUserManageTeamAsync(tournament.TeamId, userId))
            {
                return Forbid();
            }

            var activePlayers = await _context.TeamMemberships
                .Where(tm => tm.TeamId == tournament.TeamId && tm.LeftDate == null)
                .Include(tm => tm.User)
                .Include(tm => tm.GameRole)
                .Select(tm => new SelectListItem
                {
                    Value = tm.UserId,
                    Text = $"{tm.User.FullName} ({tm.GameRole.RoleName})"
                })
                .ToListAsync();

            var viewModel = new TournamentEditViewModel
            {
                Id = tournament.Id,
                TeamId = tournament.TeamId,
                TeamName = tournament.Team.Name,
                Name = tournament.Name,
                StartDate = tournament.StartDate,
                EndDate = tournament.EndDate,
                Placement = tournament.Placement,
                PrizePoolWon = tournament.PrizePoolWon,
                Notes = tournament.Notes,
                SelectedPlayerIds = tournament.Participants.Select(p => p.PlayerId).ToList(),
                AvailablePlayers = activePlayers
            };

            return View(viewModel);
        }

        // POST: /Tournament/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TournamentEditViewModel model)
        {
            if (id != model.Id) return BadRequest();

            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var tournament = await _context.Tournaments
                .Include(t => t.Participants)
                .Include(t => t.Team)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (tournament == null) return NotFound();

            if (!await CanUserManageTeamAsync(tournament.TeamId, userId))
            {
                return Forbid();
            }

            if (model.EndDate < model.StartDate)
            {
                ModelState.AddModelError("EndDate", "End date cannot be earlier than start date.");
            }

            if (!ModelState.IsValid)
            {
                model.TeamName = tournament.Team.Name;
                model.AvailablePlayers = await _context.TeamMemberships
                    .Where(tm => tm.TeamId == tournament.TeamId && tm.LeftDate == null)
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

            tournament.Name = model.Name.Trim();
            tournament.StartDate = model.StartDate;
            tournament.EndDate = model.EndDate;
            tournament.Placement = model.Placement?.Trim();
            tournament.PrizePoolWon = model.PrizePoolWon;
            tournament.Notes = model.Notes?.Trim();

            // Re-sync participants and update BaseShare calculation using PrizeCalculatorService
            _context.TournamentParticipants.RemoveRange(tournament.Participants);

            if (model.SelectedPlayerIds != null && model.SelectedPlayerIds.Count > 0)
            {
                decimal baseSharePerPlayer = _prizeCalculator.CalculateBaseShare(model.PrizePoolWon, model.SelectedPlayerIds.Count);

                foreach (var playerId in model.SelectedPlayerIds)
                {
                    _context.TournamentParticipants.Add(new TournamentParticipant
                    {
                        TournamentId = tournament.Id,
                        PlayerId = playerId,
                        BaseShare = baseSharePerPlayer
                    });
                }
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Tournament details updated successfully.";
            return RedirectToAction(nameof(Details), new { id = tournament.Id });
        }

        // POST: /Tournament/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var tournament = await _context.Tournaments.FirstOrDefaultAsync(t => t.Id == id);
            if (tournament == null) return NotFound();

            if (!await CanUserManageTeamAsync(tournament.TeamId, userId))
            {
                return Forbid();
            }

            int teamId = tournament.TeamId;
            _context.Tournaments.Remove(tournament);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Tournament deleted.";
            return RedirectToAction(nameof(Index), new { teamId });
        }
    }
}
