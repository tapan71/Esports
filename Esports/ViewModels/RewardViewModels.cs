using System.ComponentModel.DataAnnotations;
using Esports.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Esports.ViewModels
{
    public class RewardIndexViewModel
    {
        public int? TournamentId { get; set; }
        public string? TournamentName { get; set; }
        public List<TournamentReward> Rewards { get; set; } = new();
        public bool CanManage { get; set; }
    }

    public class RewardCreateViewModel
    {
        [Required]
        public int TournamentId { get; set; }
        public string TournamentName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a player.")]
        [Display(Name = "Player")]
        public string PlayerId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Reward title is required.")]
        [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
        [Display(Name = "Reward Title (e.g. MVP, Best Sniper)")]
        public string RewardTitle { get; set; } = string.Empty;

        [Range(0, 10000000, ErrorMessage = "Bonus amount must be non-negative.")]
        [Display(Name = "Bonus Amount ($)")]
        public decimal BonusAmount { get; set; } = 0;

        public IEnumerable<SelectListItem> AvailablePlayers { get; set; } = new List<SelectListItem>();
    }

    public class RewardEditViewModel
    {
        public int Id { get; set; }
        public int TournamentId { get; set; }
        public string TournamentName { get; set; } = string.Empty;
        public string PlayerId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Reward title is required.")]
        [StringLength(100, ErrorMessage = "Title cannot exceed 100 characters.")]
        [Display(Name = "Reward Title")]
        public string RewardTitle { get; set; } = string.Empty;

        [Range(0, 10000000, ErrorMessage = "Bonus amount must be non-negative.")]
        [Display(Name = "Bonus Amount ($)")]
        public decimal BonusAmount { get; set; }
    }
}
