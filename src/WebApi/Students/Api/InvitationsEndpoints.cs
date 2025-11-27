using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Auth.Entities;
using WebApi.Auth.Infrastructure.Data;
using WebApi.Students.Application;
using WebApi.Students.Entities;

namespace WebApi.Students.Api;

public static class InvitationsEndpoints
{
    public static IEndpointRouteBuilder MapInvitations(this IEndpointRouteBuilder app)
    {
        app.MapPost("/v1/teachers/classes/{classId:guid}/invite",
                async (Guid classId,
                    [FromBody] CreateInviteRequest r,
                    ClaimsPrincipal user,
                    SchoolDbContext db,
                    InviteService invites,
                    IHttpContextAccessor httpAccessor,
                    CancellationToken ct) =>
                {
                    var uid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;

                    var isOwner = await db.Classes.AnyAsync(c => c.Id == classId && c.OwnerTeacherId == uid, ct);
                    if (!isOwner) return Results.Forbid();

                    var (token, exp) = invites.CreateInvite(classId, uid, r.EmailHint);
                    var baseUrl =
                        $"{httpAccessor.HttpContext!.Request.Scheme}://{httpAccessor.HttpContext.Request.Host}";
                    var inviteUrl = $"{baseUrl}/v1/invitations/accept?token={Uri.EscapeDataString(token)}";

                    return Results.Ok(new { inviteUrl, expiresUtc = exp });
                })
            .WithTags("INVITES")
            .RequireAuthorization(p => p.RequireRole("Teacher"));

        app.MapPost("/v1/invitations/accept",
                async ([FromBody] AcceptInviteRequest r,
                    ClaimsPrincipal principal,
                    SchoolDbContext db,
                    UserManager<ApplicationUser> userManager,
                    RoleManager<ApplicationRole> roleManager,
                    InviteService invites,
                    ILoggerFactory lf,
                    CancellationToken ct) =>
                {
                    var logger = lf.CreateLogger("Invitations");

                    var payload = invites.ValidateInvite(r.Token);
                    if (payload is null)
                        return Results.BadRequest(new ProblemDetails
                        {
                            Title = "Invalid or expired invite",
                            Status = StatusCodes.Status400BadRequest,
                            Type = "https://httpstatuses.com/400"
                        });

                    var (classId, ownerTeacherId, emailHint) = payload.Value;

                    var klass = await db.Classes.SingleOrDefaultAsync(c => c.Id == classId, ct);
                    if (klass is null)
                        return Results.NotFound(new ProblemDetails
                        {
                            Title = "Class not found",
                            Status = StatusCodes.Status404NotFound,
                            Type = "https://httpstatuses.com/404"
                        });

                    var currentUserId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
                    ApplicationUser? user = null;

                    if (!string.IsNullOrWhiteSpace(currentUserId))
                    {
                        user = await userManager.FindByIdAsync(currentUserId);
                        if (user is null) return Results.Unauthorized();
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(r.Email) || string.IsNullOrWhiteSpace(r.Password))
                            return Results.BadRequest(new ProblemDetails
                            {
                                Title = "Email and Password required for anonymous accept",
                                Status = StatusCodes.Status400BadRequest
                            });

                        user = await userManager.FindByEmailAsync(r.Email!);
                        if (user is null)
                        {
                            user = new ApplicationUser { UserName = r.Email, Email = r.Email, EmailConfirmed = true };
                            var created = await userManager.CreateAsync(user, r.Password!);
                            if (!created.Succeeded)
                                return Results.BadRequest(new ProblemDetails
                                {
                                    Title = "Cannot create user",
                                    Detail = string.Join("; ", created.Errors.Select(e => e.Description)),
                                    Status = StatusCodes.Status400BadRequest
                                });

                            if (!await roleManager.RoleExistsAsync("Student"))
                                await roleManager.CreateAsync(new ApplicationRole("Student"));
                            await userManager.AddToRoleAsync(user, "Student");

                            if (!await db.StudentProfiles.AnyAsync(s => s.UserId == user.Id, ct))
                            {
                                db.StudentProfiles.Add(new StudentProfile
                                {
                                    UserId = user.Id,
                                    FirstName = r.FirstName,
                                    LastName = r.LastName
                                });
                                await db.SaveChangesAsync(ct);
                            }
                        }
                    }

                    var exists = await db.ClassMembers.AnyAsync(m => m.ClassId == classId && m.UserId == user!.Id, ct);
                    if (!exists)
                    {
                        db.ClassMembers.Add(new ClassMember
                        {
                            ClassId = classId,
                            UserId = user.Id,
                            RoleInClass = "Student",
                            JoinedUtc = DateTime.UtcNow
                        });
                        await db.SaveChangesAsync(ct);
                        logger.LogInformation("User {UserId} accepted invite to class {ClassId}", user.Id, classId);
                    }

                    return Results.Ok(new { joined = true, classId });
                })
            .WithTags("INVITES");

        return app;
    }

    public record CreateInviteRequest(string? EmailHint);

    public record AcceptInviteRequest(
        [property: Required] string Token,
        string? Email,
        string? Password,
        string? FirstName,
        string? LastName);
}