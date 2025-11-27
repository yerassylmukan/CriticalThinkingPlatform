using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Auth.Application;
using WebApi.Auth.Entities;
using WebApi.Auth.Infrastructure.Data;

namespace WebApi.Auth.Api;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuth(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/auth").WithTags("AUTH");

        g.MapPost("/register", async ([FromBody] RegisterRequest r,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            SchoolDbContext db,
            JwtTokenService tokens,
            HttpContext http,
            ILoggerFactory lf,
            CancellationToken ct) =>
        {
            var existing = await userManager.FindByEmailAsync(r.Email);
            if (existing != null)
                return Results.Conflict(new { error = "email_already_exists" });

            var user = new ApplicationUser
            {
                UserName = r.Email,
                Email = r.Email,
                EmailConfirmed = false
            };

            var create = await userManager.CreateAsync(user, r.Password);
            if (!create.Succeeded)
                return Results.BadRequest(new { error = string.Join("; ", create.Errors.Select(e => e.Description)) });

            db.StudentProfiles.Add(new StudentProfile
            {
                UserId = user.Id,
                FirstName = r.FirstName,
                LastName = r.LastName
            });
            await db.SaveChangesAsync(ct);

            await userManager.AddToRoleAsync(user, "Teacher");

            var ip = http.Connection.RemoteIpAddress?.ToString();
            var ua = http.Request.Headers.UserAgent.ToString();
            var (access, refresh) = await tokens.IssueAsync(user, ip, ua, ct);
            return Results.Ok(new AuthResponse(user.Id, user.Email!, access, refresh));
        });

        g.MapPost("/login", async ([FromBody] LoginRequest r,
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            JwtTokenService tokens,
            HttpContext http,
            CancellationToken ct) =>
        {
            var user = await userManager.FindByEmailAsync(r.Email);
            if (user is null)
                return Results.Unauthorized();

            var passOk = await userManager.CheckPasswordAsync(user, r.Password);
            if (!passOk)
                return Results.Unauthorized();

            var ip = http.Connection.RemoteIpAddress?.ToString();
            var ua = http.Request.Headers.UserAgent.ToString();
            var (access, refresh) = await tokens.IssueAsync(user, ip, ua, ct);
            return Results.Ok(new AuthResponse(user.Id, user.Email!, access, refresh));
        });

        g.MapPost("/refresh", async ([FromBody] RefreshRequest r,
            JwtTokenService tokens,
            HttpContext http,
            CancellationToken ct) =>
        {
            var ip = http.Connection.RemoteIpAddress?.ToString();
            var ua = http.Request.Headers.UserAgent.ToString();
            var rotated = await tokens.RotateAsync(r.RefreshToken, ip, ua, ct);
            return rotated is null
                ? Results.Unauthorized()
                : Results.Ok(new { rotated.Value.accessToken, rotated.Value.refreshToken });
        });

        g.MapPost("/logout", async ([FromBody] RefreshRequest r, JwtTokenService tokens, CancellationToken ct) =>
        {
            var ok = await tokens.RevokeAsync(r.RefreshToken, ct);
            return ok ? Results.NoContent() : Results.Unauthorized();
        }).RequireAuthorization();

        g.MapGet("/me", async (ClaimsPrincipal user, UserManager<ApplicationUser> userManager) =>
        {
            var uid = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue(ClaimTypes.Name);
            if (string.IsNullOrEmpty(uid)) return Results.Unauthorized();

            var u = await userManager.Users.SingleOrDefaultAsync(x => x.Id == uid);
            if (u is null) return Results.Unauthorized();

            var roles = await userManager.GetRolesAsync(u);
            return Results.Ok(new
            {
                userId = u.Id,
                email = u.Email,
                roles
            });
        }).RequireAuthorization();

        return app;
    }

    public record RegisterRequest(
        [property: EmailAddress] string Email,
        [property: MinLength(6)] string Password,
        string? FirstName,
        string? LastName);

    public record LoginRequest([property: EmailAddress] string Email, string Password);

    public record RefreshRequest(string RefreshToken);

    public record AuthResponse(string UserId, string Email, string AccessToken, string RefreshToken);
}