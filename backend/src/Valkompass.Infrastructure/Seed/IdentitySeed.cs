using Microsoft.AspNetCore.Identity;
using Valkompass.Domain.Identity;
using Valkompass.Infrastructure.Identity;

namespace Valkompass.Infrastructure.Seed;

/// <summary>Seedar roller och en bootstrap-admin (skapas bara om den inte redan finns).</summary>
public static class IdentitySeed
{
    public static async Task SeedAsync(
        RoleManager<IdentityRole<Guid>> roleManager,
        UserManager<ApplicationUser> userManager,
        string adminEmail,
        string adminPassword)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
        }

        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            var result = await userManager.CreateAsync(admin, adminPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    "Kunde inte skapa admin: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        if (!await userManager.IsInRoleAsync(admin, Roles.Admin))
            await userManager.AddToRoleAsync(admin, Roles.Admin);
    }
}
