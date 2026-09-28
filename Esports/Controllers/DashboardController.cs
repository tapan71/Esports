using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Esports.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        [Authorize(Roles = "Owner")]
        public IActionResult Owner() => View();

        [Authorize(Roles = "Coach")]
        public IActionResult Coach() => View();

        [Authorize(Roles = "Player")]
        public IActionResult Player() => View();
    }
}