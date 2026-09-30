using System.ComponentModel.DataAnnotations;
using Esports.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Esports.ViewModels
{
    public class ScheduleIndexViewModel
    {
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;
        public DateTime SelectedDate { get; set; } = DateTime.UtcNow.Date;
        public List<DailySchedule> Schedules { get; set; } = new();
        public bool CanManage { get; set; }
    }

    public class ScheduleCreateViewModel
    {
        [Required]
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(150, ErrorMessage = "Title cannot exceed 150 characters.")]
        public string Title { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Schedule date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime ScheduleDate { get; set; } = DateTime.UtcNow.Date;

        [Required(ErrorMessage = "Start time is required.")]
        [DataType(DataType.Time)]
        [Display(Name = "Start Time")]
        public TimeSpan StartTime { get; set; } = new TimeSpan(14, 0, 0);

        [Required(ErrorMessage = "End time is required.")]
        [DataType(DataType.Time)]
        [Display(Name = "End Time")]
        public TimeSpan EndTime { get; set; } = new TimeSpan(16, 0, 0);
    }

    public class ScheduleEditViewModel
    {
        public int Id { get; set; }

        [Required]
        public int TeamId { get; set; }
        public string TeamName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Title is required.")]
        [StringLength(150, ErrorMessage = "Title cannot exceed 150 characters.")]
        public string Title { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Schedule date is required.")]
        [DataType(DataType.Date)]
        [Display(Name = "Date")]
        public DateTime ScheduleDate { get; set; }

        [Required(ErrorMessage = "Start time is required.")]
        [DataType(DataType.Time)]
        [Display(Name = "Start Time")]
        public TimeSpan StartTime { get; set; }

        [Required(ErrorMessage = "End time is required.")]
        [DataType(DataType.Time)]
        [Display(Name = "End Time")]
        public TimeSpan EndTime { get; set; }
    }
}
