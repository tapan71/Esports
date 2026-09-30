using System.ComponentModel.DataAnnotations;
using Esports.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Esports.ViewModels
{
    public class RosterIndexViewModel
    {
        public Team Team { get; set; } = null!;
        public List<TeamMembership> ActiveMembers { get; set; } = new();
        public List<TeamMembership> PastMembers { get; set; } = new();
        public bool CanManage { get; set; }
    }

    public class AddPlayerToRosterViewModel
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a player.")]
        [Display(Name = "Player")]
        public string UserId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a game role.")]
        [Display(Name = "In-Game Role")]
        public int GameRoleId { get; set; }

        public IEnumerable<SelectListItem> AvailablePlayers { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> AvailableRoles { get; set; } = new List<SelectListItem>();
    }

    public class EditRosterRoleViewModel
    {
        public int MembershipId { get; set; }
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a game role.")]
        [Display(Name = "In-Game Role")]
        public int GameRoleId { get; set; }

        public IEnumerable<SelectListItem> AvailableRoles { get; set; } = new List<SelectListItem>();
    }
}
