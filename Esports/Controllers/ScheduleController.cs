using System.Security.Claims;
using Esports.Data;
using Esports.Models;
using Esports.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Esports.Controllers
{
    [Authorize]
    public class ScheduleController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ScheduleController(
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

        private async Task<bool> CanUserViewTeamScheduleAsync(int teamId, string userId)
        {
            if (await CanUserManageTeamAsync(teamId, userId))
            {
                return true;
            }

            return await _context.TeamMemberships.AnyAsync(tm => tm.TeamId == teamId && tm.UserId == userId && tm.LeftDate == null);
        }

        // GET: /Schedule?teamId=5&date=2026-10-01
        [HttpGet]
        public async Task<IActionResult> Index(int teamId, DateTime? date)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            if (!await CanUserViewTeamScheduleAsync(teamId, userId))
            {
                return Forbid();
            }

            var team = await _context.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == teamId);
            if (team == null) return NotFound();

            var targetDate = date?.Date ?? DateTime.UtcNow.Date;

            var schedules = await _context.DailySchedules
                .Include(ds => ds.CreatedByUser)
                .Where(ds => ds.TeamId == teamId && ds.ScheduleDate.Date == targetDate)
                .OrderBy(ds => ds.StartTime)
                .AsNoTracking()
                .ToListAsync();

            var viewModel = new ScheduleIndexViewModel
            {
                TeamId = team.Id,
                TeamName = team.Name,
                SelectedDate = targetDate,
                Schedules = schedules,
                CanManage = await CanUserManageTeamAsync(teamId, userId)
            };

            return View(viewModel);
        }

        // GET: /Schedule/Create?teamId=5
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

            var viewModel = new ScheduleCreateViewModel
            {
                TeamId = team.Id,
                TeamName = team.Name,
                ScheduleDate = DateTime.UtcNow.Date
            };

            return View(viewModel);
        }

        // POST: /Schedule/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ScheduleCreateViewModel model)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            if (!await CanUserManageTeamAsync(model.TeamId, userId))
            {
                return Forbid();
            }

            if (model.EndTime <= model.StartTime)
            {
                ModelState.AddModelError("EndTime", "End time must be later than start time.");
            }

            if (!ModelState.IsValid)
            {
                var team = await _context.Teams.AsNoTracking().FirstOrDefaultAsync(t => t.Id == model.TeamId);
                model.TeamName = team?.Name ?? string.Empty;
                return View(model);
            }

            var schedule = new DailySchedule
            {
                TeamId = model.TeamId,
                CreatedByUserId = userId,
                Title = model.Title.Trim(),
                Description = model.Description?.Trim(),
                ScheduleDate = model.ScheduleDate.Date,
                StartTime = model.StartTime,
                EndTime = model.EndTime,
                CreatedAt = DateTime.UtcNow
            };

            _context.DailySchedules.Add(schedule);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Schedule item added successfully.";
            return RedirectToAction(nameof(Index), new { teamId = model.TeamId, date = model.ScheduleDate.ToString("yyyy-MM-dd") });
        }

        // GET: /Schedule/Edit/5
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var schedule = await _context.DailySchedules
                .Include(ds => ds.Team)
                .FirstOrDefaultAsync(ds => ds.Id == id);

            if (schedule == null) return NotFound();

            if (!await CanUserManageTeamAsync(schedule.TeamId, userId))
            {
                return Forbid();
            }

            var viewModel = new ScheduleEditViewModel
            {
                Id = schedule.Id,
                TeamId = schedule.TeamId,
                TeamName = schedule.Team.Name,
                Title = schedule.Title,
                Description = schedule.Description,
                ScheduleDate = schedule.ScheduleDate,
                StartTime = schedule.StartTime,
                EndTime = schedule.EndTime
            };

            return View(viewModel);
        }

        // POST: /Schedule/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ScheduleEditViewModel model)
        {
            if (id != model.Id) return BadRequest();

            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var schedule = await _context.DailySchedules
                .Include(ds => ds.Team)
                .FirstOrDefaultAsync(ds => ds.Id == id);

            if (schedule == null) return NotFound();

            if (!await CanUserManageTeamAsync(schedule.TeamId, userId))
            {
                return Forbid();
            }

            if (model.EndTime <= model.StartTime)
            {
                ModelState.AddModelError("EndTime", "End time must be later than start time.");
            }

            if (!ModelState.IsValid)
            {
                model.TeamName = schedule.Team.Name;
                return View(model);
            }

            schedule.Title = model.Title.Trim();
            schedule.Description = model.Description?.Trim();
            schedule.ScheduleDate = model.ScheduleDate.Date;
            schedule.StartTime = model.StartTime;
            schedule.EndTime = model.EndTime;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Schedule item updated successfully.";
            return RedirectToAction(nameof(Index), new { teamId = schedule.TeamId, date = schedule.ScheduleDate.ToString("yyyy-MM-dd") });
        }

        // POST: /Schedule/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Challenge();

            var schedule = await _context.DailySchedules.FirstOrDefaultAsync(ds => ds.Id == id);
            if (schedule == null) return NotFound();

            if (!await CanUserManageTeamAsync(schedule.TeamId, userId))
            {
                return Forbid();
            }

            int teamId = schedule.TeamId;
            var date = schedule.ScheduleDate;

            _context.DailySchedules.Remove(schedule);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Schedule item removed.";
            return RedirectToAction(nameof(Index), new { teamId, date = date.ToString("yyyy-MM-dd") });
        }
    }
}
