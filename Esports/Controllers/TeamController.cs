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
    public class TeamController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public TeamController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private string? GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

        private async Task<bool> IsUserTeamOwnerAsync(int teamId, string userId)
        {
            return await _context.Teams
                .AnyAsync(t => t.Id == teamId && t.OwnerId == userId);
        }

        private async Task<bool> IsUserActiveCoachAsync(int teamId, string userId)
        {
            return await _context.TeamStaff
                .AnyAsync(ts => ts.TeamId == teamId && ts.UserId == userId && ts.RemovedDate == null);
        }

        private async Task<bool> CanUserManageTeamAsync(int teamId, string userId)
        {
            if (User.IsInRole("Owner"))
            {
                return await IsUserTeamOwnerAsync(teamId, userId);
            }

            if (User.IsInRole("Coach"))
            {
                return await IsUserActiveCoachAsync(teamId, userId);
            }

            return false;
        }

        // GET: /Team
        // Displays teams relevant to the current user based on their role
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            IQueryable<Team> query = _context.Teams
                .Include(t => t.Game)
                .Include(t => t.Owner)
                .Include(t => t.TeamStaff.Where(ts => ts.RemovedDate == null))
                    .ThenInclude(ts => ts.User)
                .Include(t => t.TeamMemberships.Where(tm => tm.LeftDate == null));

            if (User.IsInRole("Owner"))
            {
                // Owner sees teams they own
                query = query.Where(t => t.OwnerId == userId);
            }
            else if (User.IsInRole("Coach"))
            {
                // Coach sees teams where they are active staff
                query = query.Where(t => t.TeamStaff.Any(ts => ts.UserId == userId && ts.RemovedDate == null));
            }
            else if (User.IsInRole("Player"))
            {
                // Player sees teams where they are currently an active member
                query = query.Where(t => t.TeamMemberships.Any(tm => tm.UserId == userId && tm.LeftDate == null));
            }

            var teams = await query.AsNoTracking().ToListAsync();
            return View(teams);
        }

        // GET: /Team/Details/5
        // Displays full team details, active coach, active roster, and history if authorized
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var team = await _context.Teams
                .Include(t => t.Game)
                .Include(t => t.Owner)
                .Include(t => t.TeamStaff)
                    .ThenInclude(ts => ts.User)
                .Include(t => t.TeamMemberships)
                    .ThenInclude(tm => tm.User)
                .Include(t => t.TeamMemberships)
                    .ThenInclude(tm => tm.GameRole)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (team == null)
            {
                return NotFound();
            }

            // Authorization check: Verify if user belongs to this team (Owner, active Coach, or active Player)
            bool isOwner = team.OwnerId == userId;
            bool isCoach = team.TeamStaff.Any(ts => ts.UserId == userId && ts.RemovedDate == null);
            bool isPlayer = team.TeamMemberships.Any(tm => tm.UserId == userId && tm.LeftDate == null);

            if (!isOwner && !isCoach && !isPlayer)
            {
                return Forbid();
            }

            var viewModel = new TeamDetailsViewModel
            {
                Team = team,
                ActiveCoach = team.TeamStaff.FirstOrDefault(ts => ts.RemovedDate == null),
                ActiveRoster = team.TeamMemberships.Where(tm => tm.LeftDate == null).ToList(),
                PastRoster = team.TeamMemberships.Where(tm => tm.LeftDate != null).OrderByDescending(tm => tm.LeftDate).ToList(),
                StaffHistory = team.TeamStaff.Where(ts => ts.RemovedDate != null).OrderByDescending(ts => ts.RemovedDate).ToList(),
                CanManage = isOwner || isCoach,
                IsOwner = isOwner
            };

            return View(viewModel);
        }

        // GET: /Team/Create
        // Only Owners can create a new team
        [HttpGet]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> Create()
        {
            var games = await _context.Games
                .AsNoTracking()
                .OrderBy(g => g.Name)
                .Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.Name })
                .ToListAsync();

            var viewModel = new TeamCreateViewModel
            {
                AvailableGames = games
            };

            return View(viewModel);
        }

        // POST: /Team/Create
        [HttpPost]
        [Authorize(Roles = "Owner")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TeamCreateViewModel model)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            // Verify unique constraint: Team name per game
            bool exists = await _context.Teams
                .AnyAsync(t => t.Name == model.Name.Trim() && t.GameId == model.GameId);

            if (exists)
            {
                ModelState.AddModelError("Name", "A team with this name already exists for the selected game.");
            }

            if (!ModelState.IsValid)
            {
                model.AvailableGames = await _context.Games
                    .AsNoTracking()
                    .OrderBy(g => g.Name)
                    .Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.Name })
                    .ToListAsync();

                return View(model);
            }

            var team = new Team
            {
                Name = model.Name.Trim(),
                LogoUrl = model.LogoUrl?.Trim() ?? string.Empty,
                GameId = model.GameId,
                OwnerId = userId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Teams.Add(team);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Team '{team.Name}' created successfully.";
            return RedirectToAction(nameof(Details), new { id = team.Id });
        }

        // GET: /Team/Edit/5
        // Owners and active Coaches can edit basic team info
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            if (!await CanUserManageTeamAsync(id, userId))
            {
                return Forbid();
            }

            var team = await _context.Teams
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (team == null)
            {
                return NotFound();
            }

            var games = await _context.Games
                .AsNoTracking()
                .OrderBy(g => g.Name)
                .Select(g => new SelectListItem
                {
                    Value = g.Id.ToString(),
                    Text = g.Name,
                    Selected = g.Id == team.GameId
                })
                .ToListAsync();

            var viewModel = new TeamEditViewModel
            {
                Id = team.Id,
                Name = team.Name,
                LogoUrl = team.LogoUrl,
                GameId = team.GameId,
                AvailableGames = games
            };

            return View(viewModel);
        }

        // POST: /Team/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, TeamEditViewModel model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            if (!await CanUserManageTeamAsync(id, userId))
            {
                return Forbid();
            }

            // Check duplicate name for same game (excluding current team)
            bool duplicate = await _context.Teams
                .AnyAsync(t => t.Id != id && t.Name == model.Name.Trim() && t.GameId == model.GameId);

            if (duplicate)
            {
                ModelState.AddModelError("Name", "Another team already uses this name for the selected game.");
            }

            if (!ModelState.IsValid)
            {
                model.AvailableGames = await _context.Games
                    .AsNoTracking()
                    .OrderBy(g => g.Name)
                    .Select(g => new SelectListItem
                    {
                        Value = g.Id.ToString(),
                        Text = g.Name,
                        Selected = g.Id == model.GameId
                    })
                    .ToListAsync();

                return View(model);
            }

            var team = await _context.Teams.FirstOrDefaultAsync(t => t.Id == id);
            if (team == null)
            {
                return NotFound();
            }

            team.Name = model.Name.Trim();
            team.LogoUrl = model.LogoUrl?.Trim() ?? string.Empty;
            team.GameId = model.GameId;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Team details updated successfully.";
            return RedirectToAction(nameof(Details), new { id = team.Id });
        }

        // GET: /Team/Delete/5
        // Only Owners can delete a team
        [HttpGet]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            if (!await IsUserTeamOwnerAsync(id, userId))
            {
                return Forbid();
            }

            var team = await _context.Teams
                .Include(t => t.Game)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (team == null)
            {
                return NotFound();
            }

            return View(team);
        }

        // POST: /Team/Delete/5
        [HttpPost, ActionName("Delete")]
        [Authorize(Roles = "Owner")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            if (!await IsUserTeamOwnerAsync(id, userId))
            {
                return Forbid();
            }

            var team = await _context.Teams
                .Include(t => t.TeamStaff)
                .Include(t => t.TeamMemberships)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (team == null)
            {
                return NotFound();
            }

            // Check if there are active tournament or match dependencies before deletion
            bool hasMatches = await _context.PlayerMatchRecords.AnyAsync(pmr => pmr.TeamId == id);
            bool hasTournaments = await _context.Tournaments.AnyAsync(t => t.TeamId == id);

            if (hasMatches || hasTournaments)
            {
                TempData["ErrorMessage"] = "Cannot delete team with existing match records or tournament entries.";
                return RedirectToAction(nameof(Details), new { id });
            }

            _context.Teams.Remove(team);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Team deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Team/AssignCoach/5
        // Owner assigns a coach to the team
        [HttpGet]
        [Authorize(Roles = "Owner")]
        public async Task<IActionResult> AssignCoach(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            if (!await IsUserTeamOwnerAsync(id, userId))
            {
                return Forbid();
            }

            var team = await _context.Teams
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (team == null)
            {
                return NotFound();
            }

            // Get all coaches who are NOT currently active coaches on any team
            var coachesInRole = await _userManager.GetUsersInRoleAsync("Coach");
            var busyCoachIds = await _context.TeamStaff
                .Where(ts => ts.RemovedDate == null)
                .Select(ts => ts.UserId)
                .ToListAsync();

            var availableCoaches = coachesInRole
                .Where(c => !busyCoachIds.Contains(c.Id))
                .Select(c => new SelectListItem
                {
                    Value = c.Id,
                    Text = $"{c.FullName} ({c.Email})"
                })
                .ToList();

            var viewModel = new AssignCoachViewModel
            {
                TeamId = team.Id,
                TeamName = team.Name,
                AvailableCoaches = availableCoaches
            };

            return View(viewModel);
        }

        // POST: /Team/AssignCoach/5
        [HttpPost]
        [Authorize(Roles = "Owner")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AssignCoach(int id, AssignCoachViewModel model)
        {
            if (id != model.TeamId)
            {
                return BadRequest();
            }

            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            if (!await IsUserTeamOwnerAsync(id, userId))
            {
                return Forbid();
            }

            // Rule: A Coach can be assigned to only one team at a time
            bool isCoachBusy = await _context.TeamStaff
                .AnyAsync(ts => ts.UserId == model.CoachUserId && ts.RemovedDate == null);

            if (isCoachBusy)
            {
                ModelState.AddModelError("CoachUserId", "This coach is already actively assigned to another team.");
            }

            var coachUser = await _userManager.FindByIdAsync(model.CoachUserId);
            if (coachUser == null || !await _userManager.IsInRoleAsync(coachUser, "Coach"))
            {
                ModelState.AddModelError("CoachUserId", "Selected user is not a registered coach.");
            }

            if (!ModelState.IsValid)
            {
                var coachesInRole = await _userManager.GetUsersInRoleAsync("Coach");
                var busyCoachIds = await _context.TeamStaff
                    .Where(ts => ts.RemovedDate == null)
                    .Select(ts => ts.UserId)
                    .ToListAsync();

                model.AvailableCoaches = coachesInRole
                    .Where(c => !busyCoachIds.Contains(c.Id))
                    .Select(c => new SelectListItem
                    {
                        Value = c.Id,
                        Text = $"{c.FullName} ({c.Email})"
                    })
                    .ToList();

                return View(model);
            }

            // Archive existing active coach on this team if any (preserve history via RemovedDate)
            var currentActiveCoach = await _context.TeamStaff
                .FirstOrDefaultAsync(ts => ts.TeamId == id && ts.RemovedDate == null);

            if (currentActiveCoach != null)
            {
                currentActiveCoach.RemovedDate = DateTime.UtcNow;
            }

            // Add new staff record
            var newStaff = new TeamStaff
            {
                TeamId = id,
                UserId = model.CoachUserId,
                HiredDate = DateTime.UtcNow,
                RemovedDate = null
            };

            _context.TeamStaff.Add(newStaff);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Coach {coachUser!.FullName} has been assigned to {model.TeamName}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // POST: /Team/RemoveCoach
        // Owner removes the active coach from the team
        [HttpPost]
        [Authorize(Roles = "Owner")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveCoach(int teamId, int staffId)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            if (!await IsUserTeamOwnerAsync(teamId, userId))
            {
                return Forbid();
            }

            var staff = await _context.TeamStaff
                .Include(ts => ts.User)
                .FirstOrDefaultAsync(ts => ts.Id == staffId && ts.TeamId == teamId && ts.RemovedDate == null);

            if (staff == null)
            {
                TempData["ErrorMessage"] = "Active coach record not found.";
                return RedirectToAction(nameof(Details), new { id = teamId });
            }

            // Preserve staff history using RemovedDate
            staff.RemovedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Coach {staff.User?.FullName ?? "Staff member"} was removed from the team.";
            return RedirectToAction(nameof(Details), new { id = teamId });
        }
    }
}
