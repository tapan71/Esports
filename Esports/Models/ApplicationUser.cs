using Microsoft.AspNetCore.Identity;

namespace Esports.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;
        public string? ProfilePhotoUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public ICollection<TeamStaff> TeamStaffRoles { get; set; } = new List<TeamStaff>();
        public ICollection<TeamMembership> TeamMemberships { get; set; } = new List<TeamMembership>();
    }
}