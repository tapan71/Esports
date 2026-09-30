using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Esports.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Esports.ViewModels
{
    public class RecordIndexViewModel
    {
        public int? TeamId { get; set; }
        public string? TeamName { get; set; }
        public string? PlayerId { get; set; }
        public string? PlayerName { get; set; }
        public List<PlayerMatchRecord> Records { get; set; } = new();
        public bool CanManage { get; set; }
    }

    public class MatchStatsDto
    {
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
        public double Score { get; set; }
        public double KdaRatio => Deaths == 0 ? (Kills + Assists) : Math.Round((double)(Kills + Assists) / Deaths, 2);
    }

    public class RecordCreateViewModel
    {
        [Required]
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please select a player.")]
        [Display(Name = "Player")]
        public string PlayerId { get; set; } = string.Empty;

        [Required(ErrorMessage = "Match date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Match Date")]
        public DateTime MatchDate { get; set; } = DateTime.UtcNow.Date;

        [Display(Name = "Opponent Team")]
        [StringLength(100)]
        public string? Opponent { get; set; }

        [Required(ErrorMessage = "Match result is required.")]
        [Display(Name = "Result")]
        public string Result { get; set; } = "Win"; // Win, Loss, Draw

        [Range(0, 500, ErrorMessage = "Kills must be between 0 and 500.")]
        public int Kills { get; set; } = 0;

        [Range(0, 500, ErrorMessage = "Deaths must be between 0 and 500.")]
        public int Deaths { get; set; } = 0;

        [Range(0, 500, ErrorMessage = "Assists must be between 0 and 500.")]
        public int Assists { get; set; } = 0;

        [Range(0, 1000000, ErrorMessage = "Score must be positive.")]
        public double Score { get; set; } = 0;

        [Display(Name = "Coach Notes")]
        [StringLength(1000)]
        public string? CoachNotes { get; set; }

        public IEnumerable<SelectListItem> AvailablePlayers { get; set; } = new List<SelectListItem>();
    }

    public class RecordEditViewModel
    {
        public int Id { get; set; }
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string PlayerId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Match date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Match Date")]
        public DateTime MatchDate { get; set; }

        [Display(Name = "Opponent Team")]
        [StringLength(100)]
        public string? Opponent { get; set; }

        [Required(ErrorMessage = "Match result is required.")]
        [Display(Name = "Result")]
        public string Result { get; set; } = "Win";

        [Range(0, 500, ErrorMessage = "Kills must be between 0 and 500.")]
        public int Kills { get; set; }

        [Range(0, 500, ErrorMessage = "Deaths must be between 0 and 500.")]
        public int Deaths { get; set; }

        [Range(0, 500, ErrorMessage = "Assists must be between 0 and 500.")]
        public int Assists { get; set; }

        [Range(0, 1000000, ErrorMessage = "Score must be positive.")]
        public double Score { get; set; }

        [Display(Name = "Coach Notes")]
        [StringLength(1000)]
        public string? CoachNotes { get; set; }
    }

    public class RecordDetailsViewModel
    {
        public PlayerMatchRecord Record { get; set; } = null!;
        public MatchStatsDto Stats { get; set; } = new();
        public bool CanManage { get; set; }
    }
}
