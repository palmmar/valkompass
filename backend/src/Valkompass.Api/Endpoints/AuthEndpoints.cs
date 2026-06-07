using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Valkompass.Application.Dtos;
using Valkompass.Infrastructure.Identity;

namespace Valkompass.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (
            LoginRequest req,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.FindByEmailAsync(req.Email);
            if (user is null)
                return Results.Unauthorized();

            var result = await signInManager.PasswordSignInAsync(
                user, req.Password, isPersistent: true, lockoutOnFailure: true);
            if (!result.Succeeded)
                return Results.Unauthorized();

            var roles = await userManager.GetRolesAsync(user);
            return Results.Ok(new AuthUserDto(user.Email!, [.. roles]));
        });

        group.MapPost("/logout", async (SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapGet("/me", async (ClaimsPrincipal principal, UserManager<ApplicationUser> userManager) =>
        {
            var user = await userManager.GetUserAsync(principal);
            if (user is null)
                return Results.Unauthorized();

            var roles = await userManager.GetRolesAsync(user);
            return Results.Ok(new AuthUserDto(user.Email!, [.. roles]));
        }).RequireAuthorization();

        return app;
    }
}
