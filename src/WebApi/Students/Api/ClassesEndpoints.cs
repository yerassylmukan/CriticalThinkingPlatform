using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Auth.Infrastructure.Data;
using WebApi.Students.Entities;
using WebApi.Students.Security;

namespace WebApi.Students.Api;

public static class ClassesEndpoints
{
    public static IEndpointRouteBuilder MapClasses(this IEndpointRouteBuilder app)
    {
        var tg = app.MapGroup("/v1/teachers/classes")
            .WithTags("CLASSES")
            .RequireAuthorization(policy => policy.RequireRole("Teacher"));

        tg.MapGet("allStudents", async (SchoolDbContext db) =>
        {
            var items = await db.StudentProfiles.ToListAsync();

            return Results.Ok(items);
        });

        tg.MapGet("", async (ClaimsPrincipal user, SchoolDbContext db) =>
        {
            var uid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var items = await db.Classes
                .Where(c => c.OwnerTeacherId == uid)
                .OrderBy(c => c.Name)
                .Select(c => new ClassDto(c.Id, c.Name, c.Grade, c.Year))
                .ToListAsync();

            return Results.Ok(items);
        });

        tg.MapPost("", async ([FromBody] CreateClassRequest r, ClaimsPrincipal user, SchoolDbContext db) =>
        {
            var uid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var entity = new Class
            {
                Name = r.Name.Trim(),
                Grade = r.Grade,
                Year = r.Year,
                OwnerTeacherId = uid
            };
            db.Classes.Add(entity);
            await db.SaveChangesAsync();
            return Results.Created($"/v1/teachers/classes/{entity.Id}",
                new ClassDto(entity.Id, entity.Name, entity.Grade, entity.Year));
        });

        tg.MapGet("{classId:guid}", async (Guid classId, SchoolDbContext db) =>
        {
            var c = await db.Classes.FindAsync(classId);
            return c is null
                ? Results.NotFound()
                : Results.Ok(new ClassDto(c.Id, c.Name, c.Grade, c.Year));
        }).RequireAuthorization(p => p.AddRequirements(new ClassOwnerRequirement()));

        tg.MapPut("{classId:guid}", async (Guid classId, [FromBody] UpdateClassRequest r, SchoolDbContext db) =>
        {
            var c = await db.Classes.FindAsync(classId);
            if (c is null) return Results.NotFound();

            if (!string.IsNullOrWhiteSpace(r.Name)) c.Name = r.Name.Trim();
            c.Grade = r.Grade ?? c.Grade;
            c.Year = r.Year ?? c.Year;

            await db.SaveChangesAsync();
            return Results.Ok(new ClassDto(c.Id, c.Name, c.Grade, c.Year));
        }).RequireAuthorization(p => p.AddRequirements(new ClassOwnerRequirement()));

        tg.MapDelete("{classId:guid}", async (Guid classId, SchoolDbContext db) =>
        {
            var hasMembers = await db.ClassMembers.AnyAsync(m => m.ClassId == classId);
            if (hasMembers) return Results.Conflict(new { error = "class_not_empty" });

            var c = await db.Classes.FindAsync(classId);
            if (c is null) return Results.NotFound();

            db.Classes.Remove(c);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(p => p.AddRequirements(new ClassOwnerRequirement()));

        app.MapGet("/v1/classes/{classId:guid}/members", async (Guid classId, SchoolDbContext db) =>
            {
                var items = await db.ClassMembers
                    .Where(m => m.ClassId == classId)
                    .OrderBy(m => m.JoinedUtc)
                    .Select(m => new ClassMemberDto(m.ClassId, m.UserId, m.RoleInClass, m.JoinedUtc))
                    .ToListAsync();

                return Results.Ok(items);
            })
            .WithTags("CLASSES")
            .RequireAuthorization(p => p.AddRequirements(new ClassMemberOrOwnerRequirement()));

        tg.MapPost("{classId:guid}/members", async (Guid classId, [FromBody] AddMembersRequest r,
            SchoolDbContext db) =>
        {
            if ((r.UserIds == null || r.UserIds.Count == 0) && (r.Emails == null || r.Emails.Count == 0))
                return Results.BadRequest(new { error = "no_members_specified" });

            var toAdd = new List<string>();

            if (r.UserIds is { Count: > 0 })
            {
                var existingIds = await db.Users
                    .Where(u => r.UserIds!.Contains(u.Id))
                    .Select(u => u.Id)
                    .ToListAsync();

                toAdd.AddRange(existingIds);
            }

            if (r.Emails is { Count: > 0 })
            {
                var idsByEmail = await db.Users
                    .Where(u => r.Emails!.Contains(u.Email!))
                    .Select(u => u.Id)
                    .ToListAsync();

                toAdd.AddRange(idsByEmail);
            }

            toAdd = toAdd.Distinct().ToList();
            if (toAdd.Count == 0)
                return Results.BadRequest(new { error = "no_existing_users_found" });

            var existing = await db.ClassMembers
                .Where(m => m.ClassId == classId && toAdd.Contains(m.UserId))
                .Select(m => m.UserId)
                .ToListAsync();

            var newOnes = toAdd.Except(existing).ToList();
            if (newOnes.Count == 0) return Results.Ok(new { added = 0 });

            foreach (var uid in newOnes)
                db.ClassMembers.Add(new ClassMember
                {
                    ClassId = classId,
                    UserId = uid,
                    RoleInClass = "Student",
                    JoinedUtc = DateTime.UtcNow
                });

            await db.SaveChangesAsync();
            return Results.Ok(new { added = newOnes.Count });
        }).RequireAuthorization(p => p.AddRequirements(new ClassOwnerRequirement()));

        tg.MapDelete("{classId:guid}/members/{userId}", async (Guid classId, string userId, SchoolDbContext db) =>
        {
            var m = await db.ClassMembers.FindAsync(classId, userId);
            if (m is null) return Results.NotFound();
            db.ClassMembers.Remove(m);
            await db.SaveChangesAsync();
            return Results.NoContent();
        }).RequireAuthorization(p => p.AddRequirements(new ClassOwnerRequirement()));

        return app;
    }

    public record ClassDto(Guid Id, string Name, int? Grade, int? Year);

    public record CreateClassRequest(
        [property: Required]
        [property: MinLength(2)]
        [property: MaxLength(200)]
        string Name,
        int? Grade,
        int? Year);

    public record UpdateClassRequest(string? Name, int? Grade, int? Year);

    public record ClassMemberDto(Guid ClassId, string UserId, string RoleInClass, DateTime JoinedUtc);

    public class AddMembersRequest
    {
        public List<string>? UserIds { get; set; }
        public List<string>? Emails { get; set; }
    }
}