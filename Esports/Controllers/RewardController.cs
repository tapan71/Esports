using System.Security.Claims;
using Esports.Data;
using Esports.Models;
using Esports.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace Esports.Controllers
{
    [Authorize]
    public class RewardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public RewardController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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

        // GET: /Reward?tournamentId=5
        [HttpGet]
        public async Task<IActionResult> Index(int? tournamentId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            IQueryable<TournamentReward> query = _context.TournamentRewards
                .Include(r => r.Tournament)
                    .ThenInclude(t => t.Team)
                .Include(r => r.Player);

            if (User.IsInRole("Player"))
            {
                query = query.Where(r => r.PlayerId == userId);
            }
            else if (User.IsInRole("Coach"))
            {
                var coachedTeamIds = await _context.TeamStaff
                    .Where(ts => ts.UserId == userId && ts.RemovedDate == null)
                    .Select(ts => ts.TeamId)
                    .ToListAsync();

                query = query.Where(r => coachedTeamIds.Contains(r.Tournament.TeamId));
            }
            else if (User.IsInRole("Owner"))
            {
                var ownedTeamIds = await _context.Teams
                    .Where(t => t.OwnerId == userId)
                    .Select(t => t.Id)
                    .ToListAsync();

                query = query.Where(r => ownedTeamIds.Contains(r.Tournament.TeamId));
            }

            if (tournamentId.HasValue)
            {
                query = query.Where(r => r.TournamentId == tournamentId.Value);
            }

            var rewards = await query.AsNoTracking().ToListAsync();

            var viewModel = new RewardIndexViewModel
            {
                TournamentId = tournamentId,
                Rewards = rewards,
                CanManage = tournamentId.HasValue && await _context.Tournaments.AnyAsync(t => t.Id == tournamentId.Value && (t.Team.OwnerId == userId || t.Team.TeamStaff.Any(ts => ts.UserId == userId && ts.RemovedDate == null)))
            };

            return View(viewModel);
        }

        // GET: /Reward/Create?tournamentId=5
        [HttpGet]
        public async Task<IActionResult> Create(int tournamentId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var tournament = await _context.Tournaments
                .Include(t => t.Team)
                .Include(t => t.Participants)
                    .ThenInclude(p => p.Player)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == tournamentId);

            if (tournament == null) return NotFound();

            if (!await CanUserManageTeamAsync(tournament.TeamId, userId))
            {
                return Forbid();
            }

            // Tournament participants are prime candidates for bonus rewards
            var playerList = tournament.Participants
                .Select(p => new SelectListItem
                {
                    Value = p.PlayerId,
                    Text = p.Player.FullName
                })
                .ToList();

            // If no participants assigned yet, fallback to active team roster
            if (!playerList.Any())
            {
                playerList = await _context.TeamMemberships
                    .Where(tm => tm.TeamId == tournament.TeamId && tm.LeftDate == null)
                    .Include(tm => tm.User)
                    .Select(tm => new SelectListItem { Value = tm.UserId, Text = tm.User.FullName })
                    .ToListAsync();
            }

            var viewModel = new RewardCreateViewModel
            {
                TournamentId = tournament.Id,
                TournamentName = tournament.Name,
                AvailablePlayers = playerList
            };

            return View(viewModel);
        }

        // POST: /Reward/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RewardCreateViewModel model)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var tournament = await _context.Tournaments.FirstOrDefaultAsync(t => t.Id == model.TournamentId);
            if (tournament == null) return NotFound();

            if (!await CanUserManageTeamAsync(tournament.TeamId, userId))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                model.TournamentName = tournament.Name;
                model.AvailablePlayers = await _context.TeamMemberships
                    .Where(tm => tm.TeamId == tournament.TeamId && tm.LeftDate == null)
                    .Include(tm => tm.User)
                    .Select(tm => new SelectListItem { Value = tm.UserId, Text = tm.User.FullName })
                    .ToListAsync();

                return View(model);
            }

            var reward = new TournamentReward
            {
                TournamentId = model.TournamentId,
                PlayerId = model.PlayerId,
                RewardTitle = model.RewardTitle.Trim(),
                BonusAmount = model.BonusAmount
            };

            _context.TournamentRewards.Add(reward);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Special reward '{reward.RewardTitle}' awarded successfully.";
            return RedirectToAction("Details", "Tournament", new { id = model.TournamentId });
        }

        // GET: /Reward/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var reward = await _context.TournamentRewards
                .Include(r => r.Tournament)
                .Include(r => r.Player)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reward == null) return NotFound();

            if (!await CanUserManageTeamAsync(reward.Tournament.TeamId, userId))
            {
                return Forbid();
            }

            var viewModel = new RewardEditViewModel
            {
                Id = reward.Id,
                TournamentId = reward.TournamentId,
                TournamentName = reward.Tournament.Name,
                PlayerId = reward.PlayerId,
                PlayerName = reward.Player.FullName,
                RewardTitle = reward.RewardTitle,
                BonusAmount = reward.BonusAmount
            };

            return View(viewModel);
        }

        // POST: /Reward/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, RewardEditViewModel model)
        {
            if (id != model.Id) return BadRequest();

            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var reward = await _context.TournamentRewards
                .Include(r => r.Tournament)
                .Include(r => r.Player)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reward == null) return NotFound();

            if (!await CanUserManageTeamAsync(reward.Tournament.TeamId, userId))
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                model.TournamentName = reward.Tournament.Name;
                model.PlayerName = reward.Player.FullName;
                return View(model);
            }

            reward.RewardTitle = model.RewardTitle.Trim();
            reward.BonusAmount = model.BonusAmount;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Reward updated successfully.";
            return RedirectToAction("Details", "Tournament", new { id = reward.TournamentId });
        }

        // POST: /Reward/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var reward = await _context.TournamentRewards
                .Include(r => r.Tournament)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (reward == null) return NotFound();

            if (!await CanUserManageTeamAsync(reward.Tournament.TeamId, userId))
            {
                return Forbid();
            }

            int tournamentId = reward.TournamentId;
            _context.TournamentRewards.Remove(reward);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Reward removed.";
            return RedirectToAction("Details", "Tournament", new { id = tournamentId });
        }
    }
}
