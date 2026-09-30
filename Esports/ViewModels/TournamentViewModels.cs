using System.ComponentModel.DataAnnotations;
using Esports.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Esports.ViewModels
{
    public class TournamentIndexViewModel
    {
        public int? TeamId { get; set; }
        public string? TeamName { get; set; }
        public List<Tournament> Tournaments { get; set; } = new();
        public bool CanManage { get; set; }
    }

    public class TournamentDetailsViewModel
    {
        public Tournament Tournament { get; set; } = null!;
        public decimal TotalRewardsDistributed { get; set; }
        public bool CanManage { get; set; }
    }

    public class TournamentCreateViewModel
    {
        [Required]
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tournament name is required.")]
        [StringLength(150, ErrorMessage = "Name cannot exceed 150 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Start date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; } = DateTime.UtcNow.Date;

        [Required(ErrorMessage = "End date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; } = DateTime.UtcNow.Date;

        [Display(Name = "Final Placement")]
        [StringLength(50)]
        public string? Placement { get; set; } // e.g. "1st Place", "Semi-Finals"

        [Range(0, 100000000, ErrorMessage = "Prize pool must be non-negative.")]
        [Display(Name = "Prize Pool Won ($)")]
        public decimal PrizePoolWon { get; set; } = 0;

        [StringLength(1000)]
        public string? Notes { get; set; }

        [Display(Name = "Participating Players")]
        public List<string> SelectedPlayerIds { get; set; } = new();

        public IEnumerable<SelectListItem> AvailablePlayers { get; set; } = new List<SelectListItem>();
    }

    public class TournamentEditViewModel
    {
        public int Id { get; set; }
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Tournament name is required.")]
        [StringLength(150, ErrorMessage = "Name cannot exceed 150 characters.")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Start date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "End date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        public DateTime EndDate { get; set; }

        [Display(Name = "Final Placement")]
        [StringLength(50)]
        public string? Placement { get; set; }

        [Range(0, 100000000, ErrorMessage = "Prize pool must be non-negative.")]
        [Display(Name = "Prize Pool Won ($)")]
        public decimal PrizePoolWon { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        [Display(Name = "Participating Players")]
        public List<string> SelectedPlayerIds { get; set; } = new();

        public IEnumerable<SelectListItem> AvailablePlayers { get; set; } = new List<SelectListItem>();
    }
}
