using System.ComponentModel.DataAnnotations;
using Esports.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Esports.ViewModels
{
    public class TeamCreateViewModel
    {
        [Required(ErrorMessage = "Team name is required.")]
        [StringLength(100, ErrorMessage = "Team name cannot exceed 100 characters.")]
        [Display(Name = "Team Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Logo URL")]
        public string? LogoUrl { get; set; }

        [Display(Name = "Upload Logo Image")]
        public IFormFile? LogoFile { get; set; }

        [Required(ErrorMessage = "Please select a game.")]
        [Display(Name = "Game")]
        public int GameId { get; set; }

        public IEnumerable<SelectListItem> AvailableGames { get; set; } = new List<SelectListItem>();
    }

    public class TeamEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Team name is required.")]
        [StringLength(100, ErrorMessage = "Team name cannot exceed 100 characters.")]
        [Display(Name = "Team Name")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "Current Logo")]
        public string? CurrentLogoUrl { get; set; }

        [Display(Name = "Logo URL")]
        public string? LogoUrl { get; set; }

        [Display(Name = "Upload New Logo Image")]
        public IFormFile? LogoFile { get; set; }

        [Required(ErrorMessage = "Please select a game.")]
        [Display(Name = "Game")]
        public int GameId { get; set; }

        public IEnumerable<SelectListItem> AvailableGames { get; set; } = new List<SelectListItem>();
    }

    public class AssignCoachViewModel
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a coach.")]
        [Display(Name = "Coach")]
        public string CoachUserId { get; set; } = string.Empty;

        public IEnumerable<SelectListItem> AvailableCoaches { get; set; } = new List<SelectListItem>();
    }

    public class TeamDetailsViewModel
    {
        public Team Team { get; set; } = null!;
        public TeamStaff? ActiveCoach { get; set; }
        public List<TeamMembership> ActiveRoster { get; set; } = new();
        public List<TeamMembership> PastRoster { get; set; } = new();
        public List<TeamStaff> StaffHistory { get; set; } = new();
        public bool CanManage { get; set; }
        public bool IsOwner { get; set; }
    }
}
