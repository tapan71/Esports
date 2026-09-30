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
    public class RosterController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public RosterController(
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

        private async Task<bool> CanUserViewTeamAsync(int teamId, string userId)
        {
            if (await CanUserManageTeamAsync(teamId, userId))
            {
                return true;
            }

            // Players can view their active team roster
            return await _context.TeamMemberships.AnyAsync(tm => tm.TeamId == teamId && tm.UserId == userId && tm.LeftDate == null);
        }

        // GET: /Roster?teamId=5
        [HttpGet]
        public async Task<IActionResult> Index(int teamId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            if (!await CanUserViewTeamAsync(teamId, userId))
            {
                return Forbid();
            }

            var team = await _context.Teams
                .Include(t => t.Game)
                .Include(t => t.TeamMemberships)
                    .ThenInclude(tm => tm.User)
                .Include(t => t.TeamMemberships)
                    .ThenInclude(tm => tm.GameRole)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == teamId);

            if (team == null) return NotFound();

            var viewModel = new RosterIndexViewModel
            {
                Team = team,
                ActiveMembers = team.TeamMemberships.Where(tm => tm.LeftDate == null).OrderBy(tm => tm.GameRole.RoleName).ToList(),
                PastMembers = team.TeamMemberships.Where(tm => tm.LeftDate != null).OrderByDescending(tm => tm.LeftDate).ToList(),
                CanManage = await CanUserManageTeamAsync(teamId, userId)
            };

            return View(viewModel);
        }

        // GET: /Roster/AddPlayer?teamId=5
        [HttpGet]
        public async Task<IActionResult> AddPlayer(int teamId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            if (!await CanUserManageTeamAsync(teamId, userId))
            {
                return Forbid();
            }

            var team = await _context.Teams
                .Include(t => t.Game)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == teamId);

            if (team == null) return NotFound();

            // Active player IDs in this team to exclude from selection
            var activeMemberUserIds = await _context.TeamMemberships
                .Where(tm => tm.TeamId == teamId && tm.LeftDate == null)
                .Select(tm => tm.UserId)
                .ToListAsync();

            var allPlayers = await _userManager.GetUsersInRoleAsync("Player");
            var availablePlayers = allPlayers
                .Where(p => !activeMemberUserIds.Contains(p.Id))
                .Select(p => new SelectListItem { Value = p.Id, Text = $"{p.FullName} ({p.Email})" })
                .ToList();

            var roles = await _context.GameRoles
                .Where(gr => gr.GameId == team.GameId)
                .AsNoTracking()
                .OrderBy(gr => gr.RoleName)
                .Select(gr => new SelectListItem { Value = gr.Id.ToString(), Text = gr.RoleName })
                .ToListAsync();

            var viewModel = new AddPlayerToRosterViewModel
            {
                TeamId = team.Id,
                TeamName = team.Name,
                GameName = team.Game.Name,
                AvailablePlayers = availablePlayers,
                AvailableRoles = roles
            };

            return View(viewModel);
        }

        // POST: /Roster/AddPlayer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPlayer(AddPlayerToRosterViewModel model)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            if (!await CanUserManageTeamAsync(model.TeamId, userId))
            {
                return Forbid();
            }

            var team = await _context.Teams
                .Include(t => t.Game)
                .FirstOrDefaultAsync(t => t.Id == model.TeamId);

            if (team == null) return NotFound();

            // Validate player
            var playerUser = await _userManager.FindByIdAsync(model.UserId);
            if (playerUser == null || !await _userManager.IsInRoleAsync(playerUser, "Player"))
            {
                ModelState.AddModelError("UserId", "Selected user is not a valid player.");
            }

            // Check if player is already active on this team
            bool alreadyActive = await _context.TeamMemberships
                .AnyAsync(tm => tm.TeamId == model.TeamId && tm.UserId == model.UserId && tm.LeftDate == null);

            if (alreadyActive)
            {
                ModelState.AddModelError("UserId", "This player is already an active member of this team.");
            }

            // Validate game role belongs to team's game
            bool roleValid = await _context.GameRoles
                .AnyAsync(gr => gr.Id == model.GameRoleId && gr.GameId == team.GameId);

            if (!roleValid)
            {
                ModelState.AddModelError("GameRoleId", "The selected role does not belong to this game.");
            }

            if (!ModelState.IsValid)
            {
                var activeMemberUserIds = await _context.TeamMemberships
                    .Where(tm => tm.TeamId == model.TeamId && tm.LeftDate == null)
                    .Select(tm => tm.UserId)
                    .ToListAsync();

                var allPlayers = await _userManager.GetUsersInRoleAsync("Player");
                model.AvailablePlayers = allPlayers
                    .Where(p => !activeMemberUserIds.Contains(p.Id))
                    .Select(p => new SelectListItem { Value = p.Id, Text = $"{p.FullName} ({p.Email})" })
                    .ToList();

                model.AvailableRoles = await _context.GameRoles
                    .Where(gr => gr.GameId == team.GameId)
                    .AsNoTracking()
                    .OrderBy(gr => gr.RoleName)
                    .Select(gr => new SelectListItem { Value = gr.Id.ToString(), Text = gr.RoleName })
                    .ToListAsync();

                model.TeamName = team.Name;
                model.GameName = team.Game.Name;

                return View(model);
            }

            var membership = new TeamMembership
            {
                TeamId = model.TeamId,
                UserId = model.UserId,
                GameRoleId = model.GameRoleId,
                JoinedDate = DateTime.UtcNow,
                LeftDate = null
            };

            _context.TeamMemberships.Add(membership);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Player {playerUser!.FullName} was added to the roster.";
            return RedirectToAction(nameof(Index), new { teamId = model.TeamId });
        }

        // GET: /Roster/EditRole/5
        [HttpGet]
        public async Task<IActionResult> EditRole(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var membership = await _context.TeamMemberships
                .Include(tm => tm.Team)
                .Include(tm => tm.User)
                .Include(tm => tm.GameRole)
                .FirstOrDefaultAsync(tm => tm.Id == id && tm.LeftDate == null);

            if (membership == null) return NotFound();

            if (!await CanUserManageTeamAsync(membership.TeamId, userId))
            {
                return Forbid();
            }

            var roles = await _context.GameRoles
                .Where(gr => gr.GameId == membership.Team.GameId)
                .AsNoTracking()
                .OrderBy(gr => gr.RoleName)
                .Select(gr => new SelectListItem
                {
                    Value = gr.Id.ToString(),
                    Text = gr.RoleName,
                    Selected = gr.Id == membership.GameRoleId
                })
                .ToListAsync();

            var viewModel = new EditRosterRoleViewModel
            {
                MembershipId = membership.Id,
                TeamId = membership.TeamId,
                TeamName = membership.Team.Name,
                PlayerName = membership.User.FullName,
                GameRoleId = membership.GameRoleId,
                AvailableRoles = roles
            };

            return View(viewModel);
        }

        // POST: /Roster/EditRole/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditRole(int id, EditRosterRoleViewModel model)
        {
            if (id != model.MembershipId) return BadRequest();

            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var membership = await _context.TeamMemberships
                .Include(tm => tm.Team)
                .Include(tm => tm.User)
                .FirstOrDefaultAsync(tm => tm.Id == id && tm.LeftDate == null);

            if (membership == null) return NotFound();

            if (!await CanUserManageTeamAsync(membership.TeamId, userId))
            {
                return Forbid();
            }

            bool roleValid = await _context.GameRoles
                .AnyAsync(gr => gr.Id == model.GameRoleId && gr.GameId == membership.Team.GameId);

            if (!roleValid)
            {
                ModelState.AddModelError("GameRoleId", "Invalid role selected for this game.");
            }

            if (!ModelState.IsValid)
            {
                model.AvailableRoles = await _context.GameRoles
                    .Where(gr => gr.GameId == membership.Team.GameId)
                    .AsNoTracking()
                    .OrderBy(gr => gr.RoleName)
                    .Select(gr => new SelectListItem
                    {
                        Value = gr.Id.ToString(),
                        Text = gr.RoleName,
                        Selected = gr.Id == model.GameRoleId
                    })
                    .ToListAsync();

                model.TeamName = membership.Team.Name;
                model.PlayerName = membership.User.FullName;
                return View(model);
            }

            membership.GameRoleId = model.GameRoleId;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Role for {membership.User.FullName} updated successfully.";
            return RedirectToAction(nameof(Index), new { teamId = membership.TeamId });
        }

        // POST: /Roster/RemovePlayer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePlayer(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var membership = await _context.TeamMemberships
                .Include(tm => tm.User)
                .FirstOrDefaultAsync(tm => tm.Id == id && tm.LeftDate == null);

            if (membership == null) return NotFound();

            if (!await CanUserManageTeamAsync(membership.TeamId, userId))
            {
                return Forbid();
            }

            // Preserve roster history: Set LeftDate to UtcNow
            membership.LeftDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{membership.User.FullName} has been moved to past roster history.";
            return RedirectToAction(nameof(Index), new { teamId = membership.TeamId });
        }
    }
}
