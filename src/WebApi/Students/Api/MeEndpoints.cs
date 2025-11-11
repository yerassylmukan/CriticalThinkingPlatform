using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Auth.Entities;
using WebApi.Auth.Infrastructure.Data;

namespace WebApi.Students.Api;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMe(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1/me")
            .WithTags("ME")
            .RequireAuthorization();

        g.MapGet("/profile", async (
            ClaimsPrincipal user,
            UserManager<ApplicationUser> userManager,
            SchoolDbContext db,
            CancellationToken ct) =>
        {
            var uid = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uid)) return Results.Unauthorized();

            var u = await userManager.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == uid, ct);
            if (u is null) return Results.Unauthorized();

            var roles = await userManager.GetRolesAsync(u);

            var student = await db.StudentProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == uid, ct);
            var teacher = await db.TeacherProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == uid, ct);

            var studentClasses = await (from m in db.ClassMembers
                join c in db.Classes on m.ClassId equals c.Id
                where m.UserId == uid
                orderby c.Name
                select new
                {
                    classId = c.Id,
                    c.Name,
                    c.Grade,
                    c.Year,
                    roleInClass = m.RoleInClass,
                    joinedUtc = m.JoinedUtc
                }).ToListAsync(ct);

            var teacherClasses = await db.Classes
                .Where(c => c.OwnerTeacherId == uid)
                .OrderBy(c => c.Name)
                .Select(c => new
                {
                    classId = c.Id,
                    c.Name,
                    c.Grade,
                    c.Year,
                    owner = true
                })
                .ToListAsync(ct);

            return Results.Ok(new
            {
                userId = u.Id,
                email = u.Email,
                roles,
                studentProfile = student is null
                    ? null
                    : new
                    {
                        student.FirstName,
                        student.LastName,
                        student.BirthDate,
                        student.AvatarUrl,
                        student.Bio
                    },
                teacherProfile = teacher is null
                    ? null
                    : new
                    {
                        teacher.FirstName,
                        teacher.LastName,
                        teacher.Department
                    },
                classes = new
                {
                    asStudent = studentClasses,
                    asTeacher = teacherClasses
                }
            });
        });

        g.MapPut("/profile", async (
            ClaimsPrincipal user,
            [FromBody] UpdateMyStudentProfileRequest r,
            SchoolDbContext db,
            UserManager<ApplicationUser> userManager,
            CancellationToken ct) =>
        {
            var uid = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uid)) return Results.Unauthorized();

            var u = await userManager.Users.SingleOrDefaultAsync(x => x.Id == uid, ct);
            if (u is null) return Results.Unauthorized();

            var isStudent = await userManager.IsInRoleAsync(u, "Student");
            if (!isStudent) return Results.Forbid();

            var sp = await db.StudentProfiles.SingleOrDefaultAsync(x => x.UserId == uid, ct);
            if (sp is null)
            {
                sp = new StudentProfile { UserId = uid };
                db.StudentProfiles.Add(sp);
            }

            if (r.FirstName is not null) sp.FirstName = r.FirstName;
            if (r.LastName is not null) sp.LastName = r.LastName;
            if (r.BirthDate.HasValue) sp.BirthDate = r.BirthDate;
            if (r.AvatarUrl is not null) sp.AvatarUrl = r.AvatarUrl;
            if (r.Bio is not null) sp.Bio = r.Bio;

            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        return app;
    }

    public record UpdateMyStudentProfileRequest(
        string? FirstName,
        string? LastName,
        DateTime? BirthDate,
        [property: MaxLength(512)] string? AvatarUrl,
        [property: MaxLength(1024)] string? Bio);
}