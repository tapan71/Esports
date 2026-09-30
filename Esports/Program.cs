using Esports.Data;
using Esports.Models;
using Esports.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// ASP.NET Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Application Services DI
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPrizeCalculatorService, PrizeCalculatorService>();
builder.Services.AddScoped<IStatsCalculatorService, StatsCalculatorService>();

// Where to send users who are not logged in / not allowed
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Seed Owner, Coach and Player roles and initial game data (League of Legends)
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();
    var dbContext = scope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();

    // 1. Roles
    string[] roles = { "Owner", "Coach", "Player" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // 2. Initial Supported Game (League of Legends)
    try
    {
        if (!await dbContext.Games.AnyAsync())
        {
            var lol = new Game
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
            dbContext.Games.Add(lol);
            await dbContext.SaveChangesAsync();
        }
    }
    catch
    {
        // Database migration may not have been run yet during design/testing
    }
}

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();