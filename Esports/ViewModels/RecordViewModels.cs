using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Esports.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Esports.ViewModels
{
    public class TeamMatchGroupViewModel
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string Opponent { get; set; } = string.Empty;
        public DateTime MatchDate { get; set; }
        public string Result { get; set; } = "Win";
        public string? CoachNotes { get; set; }
        public string? LoggedByName { get; set; }

        public List<PlayerMatchRecordItemViewModel> Players { get; set; } = new();

        public int TotalKills => Players.Sum(p => p.Kills);
        public int TotalDeaths => Players.Sum(p => p.Deaths);
        public int TotalAssists => Players.Sum(p => p.Assists);
        public double TotalScore => Players.Sum(p => p.Score);
    }

    public class PlayerMatchRecordItemViewModel
    {
        public int RecordId { get; set; }
        public string PlayerId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int Assists { get; set; }
        public double Score { get; set; }
        public bool IsMvp { get; set; }
        public double KdaRatio => Deaths == 0 ? (Kills + Assists) : Math.Round((double)(Kills + Assists) / Deaths, 2);
    }

    public class RecordIndexViewModel
    {
        public int? TeamId { get; set; }
        public string? TeamName { get; set; }
        public string? PlayerId { get; set; }
        public string? PlayerName { get; set; }
        public List<PlayerMatchRecord> Records { get; set; } = new();
        public List<TeamMatchGroupViewModel> TeamMatches { get; set; } = new();
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

    public class PlayerMatchStatInputModel
    {
        public string PlayerId { get; set; } = string.Empty;
        public string PlayerName { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;

        [Range(0, 500, ErrorMessage = "Kills must be between 0 and 500.")]
        public int Kills { get; set; } = 0;

        [Range(0, 500, ErrorMessage = "Deaths must be between 0 and 500.")]
        public int Deaths { get; set; } = 0;

        [Range(0, 500, ErrorMessage = "Assists must be between 0 and 500.")]
        public int Assists { get; set; } = 0;

        [Range(0, 1000000, ErrorMessage = "Score must be positive.")]
        public double Score { get; set; } = 0;

        public bool IsMvp { get; set; } = false;
    }

    public class RecordCreateViewModel
    {
        [Required]
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Match date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Match Date")]
        public DateTime MatchDate { get; set; } = DateTime.UtcNow.Date;

        [Required(ErrorMessage = "Opponent team is required.")]
        [Display(Name = "Opponent Team")]
        [StringLength(100, ErrorMessage = "Opponent name cannot exceed 100 characters.")]
        public string Opponent { get; set; } = string.Empty;

        [Required(ErrorMessage = "Match result is required.")]
        [Display(Name = "Match Result")]
        public string Result { get; set; } = "Win"; // Win, Loss, Draw

        [Display(Name = "Coach Notes & Match Review")]
        [StringLength(1000)]
        public string? CoachNotes { get; set; }

        public List<PlayerMatchStatInputModel> PlayerStats { get; set; } = new();
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
