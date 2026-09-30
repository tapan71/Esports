using Esports.Models;
using Esports.Services;
using Esports.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Esports.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailService _emailService;

        private static readonly string[] AllowedRoles = { "Owner", "Coach", "Player" };

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IEmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
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

            var user = new ApplicationUser
            {
                FullName = model.FullName.Trim(),
                Email = model.Email.Trim(),
                UserName = model.Email.Trim(),
                PhoneNumber = model.PhoneNumber?.Trim()
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
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                var user = await _userManager.FindByEmailAsync(model.Email);
                return await RedirectByRole(user!);
            }

            ModelState.AddModelError("", "Invalid email or password.");
            return View(model);
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}