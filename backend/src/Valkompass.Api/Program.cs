using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Valkompass.Api.Endpoints;
using Valkompass.Domain.Identity;
using Valkompass.Infrastructure;
using Valkompass.Infrastructure.Identity;
using Valkompass.Infrastructure.Persistence;
using Valkompass.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Anslutningssträngen 'Default' saknas.");

var frontendOrigin = builder.Configuration["FrontendOrigin"] ?? "http://localhost:3000";
const string CorsPolicy = "frontend";

builder.Services.AddInfrastructure(connectionString);

// Cookie-baserad ASP.NET Core Identity med roller (admingränssnittet).
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.Cookie.Name = "valkompass.admin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        // API: returnera statuskoder i stället för att redirecta.
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("EditorOrAdmin", p => p.RequireRole(Roles.Admin, Roles.Editor))
    .AddPolicy("AdminOnly", p => p.RequireRole(Roles.Admin));

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
    options.AddPolicy(CorsPolicy, policy =>
        policy.WithOrigins(frontendOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials())); // krävs för cookie-baserad admin-auth

// Enkel rate limiting för inlämningar (motverkar missbruk).
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("submit", o =>
    {
        o.PermitLimit = 15;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Tillämpa migrations samt seeda innehåll, roller och bootstrap-admin i utvecklingsläge.
    using (var scope = app.Services.CreateScope())
    {
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        await SeedData.SeedContentAsync(db);

        var roleManager = sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = sp.GetRequiredService<UserManager<ApplicationUser>>();
        var adminEmail = builder.Configuration["AdminUser:Email"] ?? "admin@valkompass.local";
        var adminPassword = builder.Configuration["AdminUser:Password"] ?? "Admin123!";
        await IdentitySeed.SeedAsync(roleManager, userManager, adminEmail, adminPassword);
    }

    app.MapOpenApi();
    app.MapScalarApiReference(); // UI på /scalar/v1
}

app.UseCors(CorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapPublicEndpoints();
app.MapAuthEndpoints();
app.MapAdminEndpoints();

app.Run();

// Exponeras för WebApplicationFactory i integrationstesterna.
public partial class Program;
