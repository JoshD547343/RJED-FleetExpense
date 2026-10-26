using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RJED_FleetExpense.Data;
using RJED_FleetExpense.Services;

var builder = WebApplication.CreateBuilder(args);

// === Services ===
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<BudgetService>();

// === System Settings + Backup ===
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ISettingsService, SettingsService>();
builder.Services.AddSingleton<IBackupService, BackupService>();
builder.Services.AddHostedService<AutoBackupService>();

// === Database ===
// Uses appsettings.json locally and appsettings.Production.json
// when the hosting environment is Production.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// === Identity ===
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.SignIn.RequireConfirmedAccount = false;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 99;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromSeconds(30);
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// === PayMongo ===
builder.Services.Configure<PayMongoOptions>(
    builder.Configuration.GetSection("PayMongo"));

builder.Services.AddHttpClient<
    IPayMongoCheckoutService,
    PayMongoCheckoutService>();

// === Cookie configuration (with session timeout from System Settings) ===
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/Login";
    options.SlidingExpiration = true;

    options.Events.OnValidatePrincipal = async ctx =>
    {
        var s = await ctx.HttpContext.RequestServices
            .GetRequiredService<ISettingsService>().GetAsync();
        var issued = ctx.Properties.IssuedUtc;
        if (issued != null &&
            DateTimeOffset.UtcNow - issued.Value > TimeSpan.FromMinutes(s.SessionTimeoutMinutes))
        {
            ctx.RejectPrincipal();
            await ctx.HttpContext.SignOutAsync();
        }
    };
});

// === Razor Pages ===
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Dashboard");
});

var app = builder.Build();

// === DATABASE MIGRATION ===
// Stop startup if the database connection or migration fails.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();

        db.Database.Migrate();

        logger.LogInformation("Database migrated successfully.");
    }
    catch (Exception ex)
    {
        logger.LogCritical(
            ex,
            "Database migration failed. Application startup cannot continue.");

        throw;
    }
}

// === DATABASE SEEDING: ROLES + DEFAULT ADMIN ===
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();

    var roleManager =
        services.GetRequiredService<RoleManager<IdentityRole>>();

    var userManager =
        services.GetRequiredService<UserManager<ApplicationUser>>();

    logger.LogInformation("Starting database seeding...");

    // Create roles if they do not exist.
    string[] roles =
    {
        "Super_Admin",
        "Employee",
        "Admin",
        "Supervisor",
        "Department_head",
        "Accounting"
    };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var roleResult =
                await roleManager.CreateAsync(new IdentityRole(role));

            if (roleResult.Succeeded)
            {
                logger.LogInformation("Role created: {Role}", role);
            }
            else
            {
                logger.LogError(
                    "Failed to create role {Role}: {Errors}",
                    role,
                    string.Join(
                        ", ",
                        roleResult.Errors.Select(e => e.Description)));

                throw new InvalidOperationException(
                    $"Failed to create role '{role}'.");
            }
        }
    }

    // Create the default administrator if one does not exist.
    var adminEmail = "admin@rijed.com";
    var admin = await userManager.FindByEmailAsync(adminEmail);

    if (admin == null)
    {
        admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FullName = "Admin",
            EmailConfirmed = true,
            IsActive = true
        };

        // Prefer configuring this password securely for production.
        var result = await userManager.CreateAsync(
            admin,
            "Admin@123456");

        if (result.Succeeded)
        {
            var addRoleResult =
                await userManager.AddToRoleAsync(admin, "Admin");

            if (!addRoleResult.Succeeded)
            {
                logger.LogError(
                    "Failed to assign Admin role: {Errors}",
                    string.Join(
                        ", ",
                        addRoleResult.Errors.Select(e => e.Description)));

                throw new InvalidOperationException(
                    "Failed to assign the Admin role to the default user.");
            }

            logger.LogInformation(
                "Default administrator created successfully.");
        }
        else
        {
            logger.LogError(
                "Failed to create default administrator: {Errors}",
                string.Join(
                    ", ",
                    result.Errors.Select(e => e.Description)));

            throw new InvalidOperationException(
                "Failed to create the default administrator.");
        }
    }
    else
    {
        logger.LogInformation(
            "Default administrator already exists.");
    }
}

// === MIDDLEWARE PIPELINE ===
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// === Maintenance mode (System Settings) ===
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path;
    bool exempt = path.StartsWithSegments("/Identity")
               || path.StartsWithSegments("/lib") || path.StartsWithSegments("/css")
               || path.StartsWithSegments("/js") || path.StartsWithSegments("/images");

    if (!exempt && !(ctx.User.IsInRole("Admin") || ctx.User.IsInRole("Super_Admin")))
    {
        var s = await ctx.RequestServices.GetRequiredService<ISettingsService>().GetAsync();
        if (s.MaintenanceMode)
        {
            ctx.Response.StatusCode = 503;
            await ctx.Response.WriteAsync(s.MaintenanceMessage);
            return;
        }
    }
    await next();
});

app.MapGet("/", () => Results.Redirect("/Identity/Account/Login"));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

app.Run();