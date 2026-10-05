using Esports.Models;
using Esports.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Esports.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            // 0. Automatically apply any pending migrations (e.g. TeamLogos table)
            await context.Database.MigrateAsync();

            // 1. Roles
            string[] roles = { "Owner", "Coach", "Player" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // 2. Games and Roles
            var lol = await context.Games
                .Include(g => g.GameRoles)
                .FirstOrDefaultAsync(g => g.Name == "League of Legends");

            if (lol == null)
            {
                lol = new Game
                {
                    Name = "League of Legends",
                    GameRoles = new List<GameRole>
                    {
                        new GameRole { RoleName = "Top" },
                        new GameRole { RoleName = "Jungle" },
                        new GameRole { RoleName = "Mid" },
                        new GameRole { RoleName = "ADC" },
                        new GameRole { RoleName = "Support" }
                    }
                };
                context.Games.Add(lol);
                await context.SaveChangesAsync();
            }

            var val = await context.Games
                .Include(g => g.GameRoles)
                .FirstOrDefaultAsync(g => g.Name == "Valorant");

            if (val == null)
            {
                val = new Game
                {
                    Name = "Valorant",
                    GameRoles = new List<GameRole>
                    {
                        new GameRole { RoleName = "Duelist" },
                        new GameRole { RoleName = "Initiator" },
                        new GameRole { RoleName = "Controller" },
                        new GameRole { RoleName = "Sentinel" }
                    }
                };
                context.Games.Add(val);
                await context.SaveChangesAsync();
            }

            // Quick lookup for roles
            var roleDict = await context.GameRoles
                .Where(r => r.GameId == lol.Id)
                .ToDictionaryAsync(r => r.RoleName, r => r.Id);

            // 3. Seed Users (Password: Password123!)
            const string defaultPassword = "Password123!";
            const string defaultUserAvatarUrl = "/images/default-user-avatar.svg";

            async Task<ApplicationUser> EnsureUser(string email, string fullName, string roleName)
            {
                var user = await userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        FullName = fullName,
                        EmailConfirmed = true,
                        ProfilePhotoUrl = defaultUserAvatarUrl,
                        CreatedAt = DateTime.UtcNow
                    };
                    var result = await userManager.CreateAsync(user, defaultPassword);
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, roleName);
                    }
                }
                else if (string.IsNullOrEmpty(user.ProfilePhotoUrl))
                {
                    user.ProfilePhotoUrl = defaultUserAvatarUrl;
                    await userManager.UpdateAsync(user);
                }
                return user;
            }

            // Owners
            var ownerT1 = await EnsureUser("owner@t1.com", "Joe Marsh", "Owner");
            var ownerG2 = await EnsureUser("owner@g2.com", "Alban Dechelotte", "Owner");

            // Coaches
            var coachT1 = await EnsureUser("coach@t1.com", "Kim 'kkOma' Jeong-gyun", "Coach");
            var coachG2 = await EnsureUser("coach@g2.com", "Dylan Falco", "Coach");

            // Players
            var faker = await EnsureUser("faker@t1.com", "Lee 'Faker' Sang-hyeok", "Player");
            var zeus = await EnsureUser("zeus@t1.com", "Choi 'Zeus' Woo-je", "Player");
            var oner = await EnsureUser("oner@t1.com", "Mun 'Oner' Hyeon-jun", "Player");
            var gumayusi = await EnsureUser("gumayusi@t1.com", "Lee 'Gumayusi' Min-hyeong", "Player");
            var keria = await EnsureUser("keria@t1.com", "Ryu 'Keria' Min-seok", "Player");
            var caps = await EnsureUser("caps@g2.com", "Rasmus 'Caps' Winther", "Player");

            // Ensure any existing registered users without avatar receive default avatar
            var usersWithoutAvatar = await userManager.Users
                .Where(u => string.IsNullOrEmpty(u.ProfilePhotoUrl))
                .ToListAsync();
            foreach (var u in usersWithoutAvatar)
            {
                u.ProfilePhotoUrl = defaultUserAvatarUrl;
                await userManager.UpdateAsync(u);
            }

            const string defaultTeamLogoUrl = "/images/default-team-logo.svg";

            // 4. Teams
            var teamT1 = await context.Teams.FirstOrDefaultAsync(t => t.Name == "T1");
            if (teamT1 == null)
            {
                teamT1 = new Team
                {
                    Name = "T1",
                    GameId = lol.Id,
                    OwnerId = ownerT1.Id,
                    LogoUrl = defaultTeamLogoUrl,
                    CreatedAt = DateTime.UtcNow.AddMonths(-12)
                };
                context.Teams.Add(teamT1);
                await context.SaveChangesAsync();
            }

            var teamG2 = await context.Teams.FirstOrDefaultAsync(t => t.Name == "G2 Esports");
            if (teamG2 == null)
            {
                teamG2 = new Team
                {
                    Name = "G2 Esports",
                    GameId = lol.Id,
                    OwnerId = ownerG2.Id,
                    LogoUrl = defaultTeamLogoUrl,
                    CreatedAt = DateTime.UtcNow.AddMonths(-10)
                };
                context.Teams.Add(teamG2);
                await context.SaveChangesAsync();
            }

            // Ensure all existing teams in the database use the default logo and have matching TeamLogo entities
            var allTeams = await context.Teams.Include(t => t.TeamLogo).ToListAsync();
            foreach (var team in allTeams)
            {
                team.LogoUrl = defaultTeamLogoUrl;

                if (team.TeamLogo == null)
                {
                    context.TeamLogos.Add(new TeamLogo
                    {
                        TeamId = team.Id,
                        LogoUrl = defaultTeamLogoUrl,
                        UploadedAt = team.CreatedAt
                    });
                }
                else
                {
                    team.TeamLogo.LogoUrl = defaultTeamLogoUrl;
                    team.TeamLogo.OriginalFileName = null;
                    team.TeamLogo.StoredFileName = null;
                    team.TeamLogo.ContentType = "image/svg+xml";
                    team.TeamLogo.UpdatedAt = DateTime.UtcNow;
                }
            }

            await context.SaveChangesAsync();

            // 5. Team Staff (Coach assignment)
            if (!await context.TeamStaff.AnyAsync(ts => ts.TeamId == teamT1.Id && ts.UserId == coachT1.Id))
            {
                context.TeamStaff.Add(new TeamStaff
                {
                    TeamId = teamT1.Id,
                    UserId = coachT1.Id,
                    HiredDate = DateTime.UtcNow.AddMonths(-8),
                    RemovedDate = null
                });
            }

            if (!await context.TeamStaff.AnyAsync(ts => ts.TeamId == teamG2.Id && ts.UserId == coachG2.Id))
            {
                context.TeamStaff.Add(new TeamStaff
                {
                    TeamId = teamG2.Id,
                    UserId = coachG2.Id,
                    HiredDate = DateTime.UtcNow.AddMonths(-6),
                    RemovedDate = null
                });
            }
            await context.SaveChangesAsync();

            // 6. Team Memberships (Rosters)
            var t1Roster = new List<(ApplicationUser Player, string RoleName)>
            {
                (zeus, "Top"),
                (oner, "Jungle"),
                (faker, "Mid"),
                (gumayusi, "ADC"),
                (keria, "Support")
            };

            foreach (var (player, roleName) in t1Roster)
            {
                if (!await context.TeamMemberships.AnyAsync(tm => tm.TeamId == teamT1.Id && tm.UserId == player.Id))
                {
                    if (roleDict.TryGetValue(roleName, out int roleId))
                    {
                        context.TeamMemberships.Add(new TeamMembership
                        {
                            TeamId = teamT1.Id,
                            UserId = player.Id,
                            GameRoleId = roleId,
                            JoinedDate = DateTime.UtcNow.AddMonths(-8),
                            LeftDate = null
                        });
                    }
                }
            }

            if (!await context.TeamMemberships.AnyAsync(tm => tm.TeamId == teamG2.Id && tm.UserId == caps.Id))
            {
                if (roleDict.TryGetValue("Mid", out int midRoleId))
                {
                    context.TeamMemberships.Add(new TeamMembership
                    {
                        TeamId = teamG2.Id,
                        UserId = caps.Id,
                        GameRoleId = midRoleId,
                        JoinedDate = DateTime.UtcNow.AddMonths(-6),
                        LeftDate = null
                    });
                }
            }
            await context.SaveChangesAsync();

            // 7. Daily Schedules for T1
            if (!await context.DailySchedules.AnyAsync(ds => ds.TeamId == teamT1.Id))
            {
                var today = DateTime.UtcNow.Date;
                context.DailySchedules.AddRange(
                    new DailySchedule
                    {
                        TeamId = teamT1.Id,
                        CreatedByUserId = coachT1.Id,
                        Title = "VOD Review: LCK Grand Finals",
                        Description = "Analyze baron setup and mid lane wave management vs Gen.G.",
                        ScheduleDate = today,
                        StartTime = new TimeSpan(10, 0, 0),
                        EndTime = new TimeSpan(12, 0, 0)
                    },
                    new DailySchedule
                    {
                        TeamId = teamT1.Id,
                        CreatedByUserId = coachT1.Id,
                        Title = "Scrims vs Hanwha Life (Bo5)",
                        Description = "Focus on early dragon control and bot lane dive coordination.",
                        ScheduleDate = today,
                        StartTime = new TimeSpan(14, 0, 0),
                        EndTime = new TimeSpan(18, 0, 0)
                    },
                    new DailySchedule
                    {
                        TeamId = teamT1.Id,
                        CreatedByUserId = coachT1.Id,
                        Title = "Gym & Physical Conditioning",
                        Description = "Stretching, posture exercises, and cardio with team trainer.",
                        ScheduleDate = today,
                        StartTime = new TimeSpan(19, 0, 0),
                        EndTime = new TimeSpan(20, 30, 0)
                    },
                    new DailySchedule
                    {
                        TeamId = teamT1.Id,
                        CreatedByUserId = coachT1.Id,
                        Title = "Tactical Draft Practice",
                        Description = "Draft simulations for upcoming Worlds group stage.",
                        ScheduleDate = today.AddDays(1),
                        StartTime = new TimeSpan(14, 0, 0),
                        EndTime = new TimeSpan(17, 0, 0)
                    }
                );
                await context.SaveChangesAsync();
            }

            // 8. Tournaments & Rewards
            var worlds = await context.Tournaments
                .Include(t => t.Participants)
                .Include(t => t.Rewards)
                .FirstOrDefaultAsync(t => t.Name == "World Championship 2024" && t.TeamId == teamT1.Id);

            if (worlds == null)
            {
                worlds = new Tournament
                {
                    Name = "World Championship 2024",
                    TeamId = teamT1.Id,
                    StartDate = DateTime.UtcNow.AddMonths(-2),
                    EndDate = DateTime.UtcNow.AddMonths(-1),
                    Placement = "1st Place (Champion)",
                    PrizePoolWon = 450000m,
                    Notes = "Grand Finals victory 3-2. Dominant performance.",
                    LoggedByUserId = ownerT1.Id,
                    CreatedAt = DateTime.UtcNow.AddMonths(-1)
                };

                // Add 5 players as participants
                var players = new[] { zeus, oner, faker, gumayusi, keria };
                decimal baseSharePerPlayer = 450000m / 5; // $90,000 each

                foreach (var p in players)
                {
                    worlds.Participants.Add(new TournamentParticipant
                    {
                        PlayerId = p.Id,
                        BaseShare = baseSharePerPlayer
                    });
                }

                // Add Special Rewards
                worlds.Rewards.Add(new TournamentReward
                {
                    PlayerId = faker.Id,
                    RewardTitle = "Finals MVP",
                    BonusAmount = 25000m
                });
                worlds.Rewards.Add(new TournamentReward
                {
                    PlayerId = gumayusi.Id,
                    RewardTitle = "Highest Damage Dealer",
                    BonusAmount = 10000m
                });
                worlds.Rewards.Add(new TournamentReward
                {
                    PlayerId = keria.Id,
                    RewardTitle = "Best Playmaker Award",
                    BonusAmount = 10000m
                });

                context.Tournaments.Add(worlds);
                await context.SaveChangesAsync();
            }

            // Upcoming Tournament
            if (!await context.Tournaments.AnyAsync(t => t.Name == "Mid-Season Invitational (MSI) 2025"))
            {
                context.Tournaments.Add(new Tournament
                {
                    Name = "Mid-Season Invitational (MSI) 2025",
                    TeamId = teamT1.Id,
                    StartDate = DateTime.UtcNow.AddMonths(1),
                    EndDate = DateTime.UtcNow.AddMonths(2),
                    Placement = "Upcoming",
                    PrizePoolWon = 0m,
                    Notes = "Qualified as #1 seed from LCK Spring Split.",
                    LoggedByUserId = coachT1.Id,
                    CreatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }

            // 9. Match Performance Records (Whole Team per Match)
            var existingRecordCount = await context.PlayerMatchRecords.CountAsync(pmr => pmr.TeamId == teamT1.Id);
            if (existingRecordCount < 20)
            {
                if (existingRecordCount > 0)
                {
                    var oldRecords = await context.PlayerMatchRecords.Where(pmr => pmr.TeamId == teamT1.Id).ToListAsync();
                    context.PlayerMatchRecords.RemoveRange(oldRecords);
                    await context.SaveChangesAsync();
                }

                const string noteGenG = "Flawless flank teleports during dragon contest. Superb objective control and roam synergy.";
                const string noteBLG = "Decisive teamfight turnaround at Baron. Unstoppable split push pressure on Aatrox and deathless Sylas mid.";
                const string noteHLE = "Caught out during river rotation; lost herald contest mid-game. Need deeper vision coverage and disengage discipline.";
                const string noteFNC = "Dominant Jayce and Caitlyn poke lane pressure. Flawless dragon control and clean siege execution.";

                var matches = new List<PlayerMatchRecord>
                {
                    // --- Match 1: T1 vs Gen.G (Win) ---
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = zeus.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow.AddDays(-7),
                        Opponent = "Gen.G",
                        Result = "Win",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 5, Deaths = 2, Assists = 7, Score = 2900 }),
                        CoachNotes = noteGenG
                    },
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = oner.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow.AddDays(-7),
                        Opponent = "Gen.G",
                        Result = "Win",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 4, Deaths = 2, Assists = 12, Score = 3100 }),
                        CoachNotes = noteGenG
                    },
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = faker.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow.AddDays(-7),
                        Opponent = "Gen.G",
                        Result = "Win",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 7, Deaths = 1, Assists = 12, Score = 3450 }),
                        CoachNotes = "[Match MVP] " + noteGenG
                    },
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = gumayusi.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow.AddDays(-7),
                        Opponent = "Gen.G",
                        Result = "Win",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 11, Deaths = 2, Assists = 8, Score = 3800 }),
                        CoachNotes = noteGenG
                    },
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = keria.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow.AddDays(-7),
                        Opponent = "Gen.G",
                        Result = "Win",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 2, Deaths = 2, Assists = 16, Score = 3000 }),
                        CoachNotes = noteGenG
                    },

                    // --- Match 2: T1 vs Bilibili Gaming (Win) ---
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = zeus.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow.AddDays(-4),
                        Opponent = "Bilibili Gaming",
                        Result = "Win",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 6, Deaths = 1, Assists = 8, Score = 3200 }),
                        CoachNotes = noteBLG
                    },
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = oner.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow.AddDays(-4),
                        Opponent = "Bilibili Gaming",
                        Result = "Win",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 4, Deaths = 1, Assists = 14, Score = 3100 }),
                        CoachNotes = noteBLG
                    },
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = faker.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow.AddDays(-4),
                        Opponent = "Bilibili Gaming",
                        Result = "Win",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 9, Deaths = 0, Assists = 10, Score = 4100 }),
                        CoachNotes = "[Match MVP] " + noteBLG
                    },
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = gumayusi.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow.AddDays(-4),
                        Opponent = "Bilibili Gaming",
                        Result = "Win",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 8, Deaths = 1, Assists = 9, Score = 3600 }),
                        CoachNotes = noteBLG
                    },
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = keria.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow.AddDays(-4),
                        Opponent = "Bilibili Gaming",
                        Result = "Win",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 2, Deaths = 1, Assists = 18, Score = 3300 }),
                        CoachNotes = noteBLG
                    },

                    // --- Match 3: T1 vs Hanwha Life (Loss) ---
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = zeus.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow.AddDays(-2),
                        Opponent = "Hanwha Life",
                        Result = "Loss",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 3, Deaths = 4, Assists = 2, Score = 2200 }),
                        CoachNotes = noteHLE
                    },
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = oner.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow.AddDays(-2),
                        Opponent = "Hanwha Life",
                        Result = "Loss",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 2, Deaths = 3, Assists = 4, Score = 2100 }),
                        CoachNotes = noteHLE
                    },
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = faker.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow.AddDays(-2),
                        Opponent = "Hanwha Life",
                        Result = "Loss",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 3, Deaths = 4, Assists = 4, Score = 2100 }),
                        CoachNotes = noteHLE
                    },
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = gumayusi.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow.AddDays(-2),
                        Opponent = "Hanwha Life",
                        Result = "Loss",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 4, Deaths = 3, Assists = 3, Score = 2400 }),
                        CoachNotes = noteHLE
                    },
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = keria.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow.AddDays(-2),
                        Opponent = "Hanwha Life",
                        Result = "Loss",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 1, Deaths = 4, Assists = 5, Score = 1900 }),
                        CoachNotes = noteHLE
                    },

                    // --- Match 4: T1 vs Fnatic (Win) ---
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = zeus.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow,
                        Opponent = "Fnatic",
                        Result = "Win",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 9, Deaths = 3, Assists = 4, Score = 3400 }),
                        CoachNotes = noteFNC
                    },
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = oner.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow,
                        Opponent = "Fnatic",
                        Result = "Win",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 3, Deaths = 2, Assists = 11, Score = 3000 }),
                        CoachNotes = noteFNC
                    },
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = faker.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow,
                        Opponent = "Fnatic",
                        Result = "Win",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 8, Deaths = 1, Assists = 10, Score = 3600 }),
                        CoachNotes = "[Match MVP] " + noteFNC
                    },
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = gumayusi.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow,
                        Opponent = "Fnatic",
                        Result = "Win",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 10, Deaths = 2, Assists = 7, Score = 3700 }),
                        CoachNotes = noteFNC
                    },
                    new PlayerMatchRecord
                    {
                        TeamId = teamT1.Id,
                        PlayerId = keria.Id,
                        LoggedByUserId = coachT1.Id,
                        MatchDate = DateTime.UtcNow,
                        Opponent = "Fnatic",
                        Result = "Win",
                        StatsJson = JsonSerializer.Serialize(new MatchStatsDto { Kills = 1, Deaths = 2, Assists = 15, Score = 2900 }),
                        CoachNotes = noteFNC
                    }
                };

                context.PlayerMatchRecords.AddRange(matches);
                await context.SaveChangesAsync();
            }
        }
    }
}
