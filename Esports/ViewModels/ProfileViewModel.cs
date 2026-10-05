using System.ComponentModel.DataAnnotations;
using Esports.Models;
using Microsoft.AspNetCore.Http;

namespace Esports.ViewModels
{
    public class ProfileViewModel
    {
        public string Id { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Full Name")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Email Address")]
        public string Email { get; set; } = string.Empty;

        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        public string? CurrentProfilePhotoUrl { get; set; }

        [Display(Name = "Upload New Avatar")]
        public IFormFile? ProfilePhotoFile { get; set; }

        [Display(Name = "Reset to Default Avatar")]
        public bool ResetToDefaultAvatar { get; set; }

        public string? ProfilePhotoUrl { get; set; }

        public string Role { get; set; } = string.Empty;

        public DateTime MemberSince { get; set; }

        // Optional Password Change
        [DataType(DataType.Password)]
        [Display(Name = "Current Password")]
        public string? CurrentPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "New Password")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "New password and confirmation do not match.")]
        [Display(Name = "Confirm New Password")]
        public string? ConfirmNewPassword { get; set; }

        // Associated Esports Details
        public List<Team> OwnedTeams { get; set; } = new();
        public List<TeamStaff> StaffPositions { get; set; } = new();
        public List<TeamMembership> Memberships { get; set; } = new();
    }
}
