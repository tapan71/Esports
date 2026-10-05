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
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailService _emailService;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public const string DefaultUserAvatarUrl = "/images/default-user-avatar.svg";
        private static readonly string[] AllowedRoles = { "Owner", "Coach", "Player" };

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailService emailService,
            ApplicationDbContext context,
            IWebHostEnvironment webHostEnvironment)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        private async Task<string?> ProcessProfilePhotoUploadAsync(IFormFile? photoFile, string? photoUrl)
        {
            if (photoFile != null && photoFile.Length > 0)
            {
                var webRoot = _webHostEnvironment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var uploadsFolder = Path.Combine(webRoot, "uploads", "user-avatars");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                var ext = Path.GetExtension(photoFile.FileName);
                var storedFileName = $"{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(uploadsFolder, storedFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await photoFile.CopyToAsync(stream);
                }

                return $"/uploads/user-avatars/{storedFileName}";
            }
            else if (!string.IsNullOrWhiteSpace(photoUrl))
            {
                return photoUrl.Trim();
            }

            return null;
        }

        private async Task<IActionResult> RedirectByRole(ApplicationUser user)
        {
            if (await _userManager.IsInRoleAsync(user, "Owner"))
                return RedirectToAction("Owner", "Dashboard");

            if (await _userManager.IsInRoleAsync(user, "Coach"))
                return RedirectToAction("Coach", "Dashboard");

            if (await _userManager.IsInRoleAsync(user, "Player"))
                return RedirectToAction("Player", "Dashboard");

            return RedirectToAction("Login");
        }

        // GET: /Account/Register
        [HttpGet]
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!AllowedRoles.Contains(model.Role))
            {
                ModelState.AddModelError("Role", "Invalid role selected. Only Owner, Coach, and Player are permitted.");
                return View(model);
            }

            var avatarUrl = await ProcessProfilePhotoUploadAsync(model.ProfilePhotoFile, model.ProfilePhotoUrl);
            if (string.IsNullOrWhiteSpace(avatarUrl))
            {
                avatarUrl = DefaultUserAvatarUrl;
            }

            var user = new ApplicationUser
            {
                FullName = model.FullName.Trim(),
                Email = model.Email.Trim(),
                UserName = model.Email.Trim(),
                PhoneNumber = model.PhoneNumber?.Trim(),
                ProfilePhotoUrl = avatarUrl,
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, model.Role);
                await _emailService.SendWelcomeEmailAsync(user.Email!, user.FullName, model.Role);
                await _signInManager.SignInAsync(user, isPersistent: false);
                return await RedirectByRole(user);
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View(model);
        }

        // GET: /Account/Login
        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                    return await RedirectByRole(currentUser);
            }

            // Sanitize returnUrl: Do not redirect back to AccessDenied or Login/Logout pages
            if (!string.IsNullOrEmpty(returnUrl) &&
                (returnUrl.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase) ||
                 returnUrl.Contains("Login", StringComparison.OrdinalIgnoreCase) ||
                 returnUrl.Contains("Logout", StringComparison.OrdinalIgnoreCase)))
            {
                returnUrl = null;
            }

            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model,
            string? returnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                var user = await _userManager.FindByEmailAsync(model.Email);
                if (user == null)
                {
                    return RedirectToAction("Login");
                }

                // If returnUrl is valid and safe for this user's role, redirect there
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                    && !returnUrl.Contains("AccessDenied", StringComparison.OrdinalIgnoreCase)
                    && !returnUrl.Contains("Login", StringComparison.OrdinalIgnoreCase)
                    && !returnUrl.Contains("Logout", StringComparison.OrdinalIgnoreCase))
                {
                    bool isOwner = await _userManager.IsInRoleAsync(user, "Owner");
                    bool isCoach = await _userManager.IsInRoleAsync(user, "Coach");
                    bool isPlayer = await _userManager.IsInRoleAsync(user, "Player");

                    bool canAccess = true;
                    if (returnUrl.StartsWith("/Dashboard/Owner", StringComparison.OrdinalIgnoreCase) && !isOwner)
                        canAccess = false;
                    else if (returnUrl.StartsWith("/Dashboard/Coach", StringComparison.OrdinalIgnoreCase) && !isCoach)
                        canAccess = false;
                    else if (returnUrl.StartsWith("/Dashboard/Player", StringComparison.OrdinalIgnoreCase) && !isPlayer)
                        canAccess = false;

                    if (canAccess)
                    {
                        return Redirect(returnUrl);
                    }
                }

                return await RedirectByRole(user);
            }

            ModelState.AddModelError("", "Invalid email or password.");
            return View(model);
        }

        // GET: /Account/MyDashboard
        [HttpGet]
        public async Task<IActionResult> MyDashboard()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                    return await RedirectByRole(user);
            }

            return RedirectToAction("Login");
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        // GET: /Account/Profile
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            var roles = await _userManager.GetRolesAsync(user);
            var primaryRole = roles.FirstOrDefault() ?? "Member";

            var viewModel = new ProfileViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                PhoneNumber = user.PhoneNumber,
                CurrentProfilePhotoUrl = !string.IsNullOrEmpty(user.ProfilePhotoUrl) ? user.ProfilePhotoUrl : DefaultUserAvatarUrl,
                ProfilePhotoUrl = user.ProfilePhotoUrl,
                Role = primaryRole,
                MemberSince = user.CreatedAt
            };

            if (primaryRole == "Owner")
            {
                viewModel.OwnedTeams = await _context.Teams
                    .Include(t => t.Game)
                    .Include(t => t.TeamLogo)
                    .Where(t => t.OwnerId == user.Id)
                    .AsNoTracking()
                    .ToListAsync();
            }
            else if (primaryRole == "Coach")
            {
                viewModel.StaffPositions = await _context.TeamStaff
                    .Include(ts => ts.Team).ThenInclude(t => t.Game)
                    .Include(ts => ts.Team).ThenInclude(t => t.TeamLogo)
                    .Where(ts => ts.UserId == user.Id && ts.RemovedDate == null)
                    .AsNoTracking()
                    .ToListAsync();
            }
            else if (primaryRole == "Player")
            {
                viewModel.Memberships = await _context.TeamMemberships
                    .Include(tm => tm.Team).ThenInclude(t => t.Game)
                    .Include(tm => tm.Team).ThenInclude(t => t.TeamLogo)
                    .Include(tm => tm.GameRole)
                    .Where(tm => tm.UserId == user.Id && tm.LeftDate == null)
                    .AsNoTracking()
                    .ToListAsync();
            }

            return View(viewModel);
        }

        // POST: /Account/Profile
        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login");
            }

            async Task RepopulateProfileData(ProfileViewModel vm)
            {
                var userRoles = await _userManager.GetRolesAsync(user);
                var role = userRoles.FirstOrDefault() ?? "Member";
                vm.Role = role;
                vm.Email = user.Email ?? string.Empty;
                vm.MemberSince = user.CreatedAt;
                vm.CurrentProfilePhotoUrl = !string.IsNullOrEmpty(user.ProfilePhotoUrl) ? user.ProfilePhotoUrl : DefaultUserAvatarUrl;

                if (role == "Owner")
                {
                    vm.OwnedTeams = await _context.Teams
                        .Include(t => t.Game)
                        .Include(t => t.TeamLogo)
                        .Where(t => t.OwnerId == user.Id)
                        .AsNoTracking()
                        .ToListAsync();
                }
                else if (role == "Coach")
                {
                    vm.StaffPositions = await _context.TeamStaff
                        .Include(ts => ts.Team).ThenInclude(t => t.Game)
                        .Include(ts => ts.Team).ThenInclude(t => t.TeamLogo)
                        .Where(ts => ts.UserId == user.Id && ts.RemovedDate == null)
                        .AsNoTracking()
                        .ToListAsync();
                }
                else if (role == "Player")
                {
                    vm.Memberships = await _context.TeamMemberships
                        .Include(tm => tm.Team).ThenInclude(t => t.Game)
                        .Include(tm => tm.Team).ThenInclude(t => t.TeamLogo)
                        .Include(tm => tm.GameRole)
                        .Where(tm => tm.UserId == user.Id && tm.LeftDate == null)
                        .AsNoTracking()
                        .ToListAsync();
                }
            }

            if (!ModelState.IsValid)
            {
                await RepopulateProfileData(model);
                return View(model);
            }

            user.FullName = model.FullName.Trim();
            user.PhoneNumber = model.PhoneNumber?.Trim();

            // Avatar handling
            if (model.ResetToDefaultAvatar)
            {
                user.ProfilePhotoUrl = DefaultUserAvatarUrl;
            }
            else if (model.ProfilePhotoFile != null && model.ProfilePhotoFile.Length > 0)
            {
                var newPhotoUrl = await ProcessProfilePhotoUploadAsync(model.ProfilePhotoFile, null);
                if (!string.IsNullOrEmpty(newPhotoUrl))
                {
                    user.ProfilePhotoUrl = newPhotoUrl;
                }
            }
            else if (!string.IsNullOrWhiteSpace(model.ProfilePhotoUrl))
            {
                user.ProfilePhotoUrl = model.ProfilePhotoUrl.Trim();
            }

            // Optional password update
            if (!string.IsNullOrEmpty(model.NewPassword))
            {
                if (string.IsNullOrEmpty(model.CurrentPassword))
                {
                    ModelState.AddModelError("CurrentPassword", "Current password is required to change your password.");
                    await RepopulateProfileData(model);
                    return View(model);
                }

                var changePasswordResult = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
                if (!changePasswordResult.Succeeded)
                {
                    foreach (var error in changePasswordResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, error.Description);
                    }
                    await RepopulateProfileData(model);
                    return View(model);
                }
            }

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                await RepopulateProfileData(model);
                return View(model);
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["SuccessMessage"] = "Profile details updated successfully!";
            return RedirectToAction(nameof(Profile));
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}