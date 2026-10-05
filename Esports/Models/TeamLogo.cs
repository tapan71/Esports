namespace Esports.Models
{
    public class TeamLogo
    {
        public int Id { get; set; }

        public int TeamId { get; set; }
        public Team Team { get; set; } = null!;

        // The image URL or web-accessible path (e.g. /uploads/team-logos/xyz.png or https://...)
        public string LogoUrl { get; set; } = string.Empty;

        // Metadata for uploaded files
        public string? OriginalFileName { get; set; }
        public string? StoredFileName { get; set; }
        public string? ContentType { get; set; }
        public long? FileSizeBytes { get; set; }

        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
