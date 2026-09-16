using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Esports.Models;

namespace Esports.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }

        public DbSet<Game> Games { get; set; }
        public DbSet<GameRole> GameRoles { get; set; }
        public DbSet<Team> Teams { get; set; }
        public DbSet<TeamStaff> TeamStaff { get; set; }
        public DbSet<TeamMembership> TeamMemberships { get; set; }
        public DbSet<DailySchedule> DailySchedules { get; set; }
        public DbSet<PlayerMatchRecord> PlayerMatchRecords { get; set; }
        public DbSet<Tournament> Tournaments { get; set; }
        public DbSet<TournamentParticipant> TournamentParticipants { get; set; }
        public DbSet<TournamentReward> TournamentRewards { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ---------- Team ----------
            modelBuilder.Entity<Team>()
                .HasIndex(t => new { t.Name, t.GameId })
                .IsUnique();

            modelBuilder.Entity<Team>()
                .HasOne(t => t.Game)
                .WithMany(g => g.Teams)
                .HasForeignKey(t => t.GameId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Team>()
                .HasOne(t => t.Owner)
                .WithMany()
                .HasForeignKey(t => t.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- GameRole ----------
            modelBuilder.Entity<GameRole>()
                .HasOne(gr => gr.Game)
                .WithMany(g => g.GameRoles)
                .HasForeignKey(gr => gr.GameId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- TeamStaff ----------
            modelBuilder.Entity<TeamStaff>()
                .HasOne(ts => ts.Team)
                .WithMany(t => t.TeamStaff)
                .HasForeignKey(ts => ts.TeamId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TeamStaff>()
                .HasOne(ts => ts.User)
                .WithMany(u => u.TeamStaffRoles)
                .HasForeignKey(ts => ts.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- TeamMembership ----------
            modelBuilder.Entity<TeamMembership>()
                .HasOne(tm => tm.Team)
                .WithMany(t => t.TeamMemberships)
                .HasForeignKey(tm => tm.TeamId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TeamMembership>()
                .HasOne(tm => tm.User)
                .WithMany(u => u.TeamMemberships)
                .HasForeignKey(tm => tm.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TeamMembership>()
                .HasOne(tm => tm.GameRole)
                .WithMany()
                .HasForeignKey(tm => tm.GameRoleId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- DailySchedule ----------
            modelBuilder.Entity<DailySchedule>()
                .HasOne(ds => ds.Team)
                .WithMany(t => t.DailySchedules)
                .HasForeignKey(ds => ds.TeamId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DailySchedule>()
                .HasOne(ds => ds.CreatedByUser)
                .WithMany()
                .HasForeignKey(ds => ds.CreatedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- PlayerMatchRecord ----------
            modelBuilder.Entity<PlayerMatchRecord>()
                .HasOne(pmr => pmr.Team)
                .WithMany(t => t.PlayerMatchRecords)
                .HasForeignKey(pmr => pmr.TeamId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PlayerMatchRecord>()
                .HasOne(pmr => pmr.Player)
                .WithMany()
                .HasForeignKey(pmr => pmr.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PlayerMatchRecord>()
                .HasOne(pmr => pmr.LoggedByUser)
                .WithMany()
                .HasForeignKey(pmr => pmr.LoggedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // ---------- Tournament ----------
            modelBuilder.Entity<Tournament>()
                .HasOne(t => t.Team)
                .WithMany()
                .HasForeignKey(t => t.TeamId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Tournament>()
                .HasOne(t => t.LoggedByUser)
                .WithMany()
                .HasForeignKey(t => t.LoggedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Tournament>()
                .Property(t => t.PrizePoolWon)
                .HasColumnType("decimal(18,2)");

            // ---------- TournamentParticipant ----------
            modelBuilder.Entity<TournamentParticipant>()
                .HasOne(tp => tp.Tournament)
                .WithMany(t => t.Participants)
                .HasForeignKey(tp => tp.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TournamentParticipant>()
                .HasOne(tp => tp.Player)
                .WithMany()
                .HasForeignKey(tp => tp.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TournamentParticipant>()
                .Property(tp => tp.BaseShare)
                .HasColumnType("decimal(18,2)");

            // ---------- TournamentReward ----------
            modelBuilder.Entity<TournamentReward>()
                .HasOne(tr => tr.Tournament)
                .WithMany(t => t.Rewards)
                .HasForeignKey(tr => tr.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TournamentReward>()
                .HasOne(tr => tr.Player)
                .WithMany()
                .HasForeignKey(tr => tr.PlayerId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TournamentReward>()
                .Property(tr => tr.BonusAmount)
                .HasColumnType("decimal(18,2)");
        }
    }
}