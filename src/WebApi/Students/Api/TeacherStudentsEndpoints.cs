using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Auth.Infrastructure.Data;
using WebApi.Students.Application;

namespace WebApi.Students.Api;

public static class TeacherStudentsEndpoints
{
    public static IEndpointRouteBuilder MapTeacherStudents(this IEndpointRouteBuilder app)
    {
        var tg = app.MapGroup("/v1/teachers/students")
            .WithTags("STUDENTS")
            .RequireAuthorization(p => p.RequireRole("Teacher"));

        tg.MapGet("", async (
            [FromQuery] string? q,
            [FromQuery] Guid? classId,
            [FromQuery] int page,
            [FromQuery] int pageSize,
            ClaimsPrincipal user,
            StudentService svc,
            SchoolDbContext db,
            CancellationToken ct) =>
        {
            var uid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;

            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

            if (classId.HasValue)
            {
                var own = await db.Classes.AnyAsync(c => c.Id == classId.Value && c.OwnerTeacherId == uid, ct);
                if (!own) return Results.Forbid();
            }

            var (items, total) = await svc.SearchAsync(uid, q, classId, page, pageSize, ct);
            return Results.Ok(new { items, total, page, pageSize });
        });

        tg.MapPost("", async ([FromBody] CreateStudentRequest r,
            ClaimsPrincipal user,
            StudentService svc,
            CancellationToken ct) =>
        {
            var uid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var (ok, error, userId, temp) =
                await svc.CreateStudentAsync(uid, r.Email, r.Password, r.FirstName, r.LastName, r.Classes, ct);

            return ok
                ? Results.Created($"/v1/teachers/students/{userId}", new { userId, tempPassword = temp })
                : Results.BadRequest(new { error });
        });

        tg.MapGet("{userId}", async (string userId, StudentService svc, CancellationToken ct) =>
        {
            var s = await svc.GetAsync(userId, ct);
            return s is null ? Results.NotFound() : Results.Ok(s);
        });

        tg.MapPut("{userId}", async (string userId,
            [FromBody] UpdateStudentRequest r,
            ClaimsPrincipal user,
            StudentService svc,
            CancellationToken ct) =>
        {
            var uid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var (ok, error) =
                await svc.UpdateAsync(uid, userId, r.FirstName, r.LastName, r.ClassesAdd, r.ClassesRemove, ct);
            return ok ? Results.NoContent() :
                error == "not_found" ? Results.NotFound() : Results.BadRequest(new { error });
        });

        tg.MapPost("{userId}/deactivate", async (string userId, StudentService svc, CancellationToken ct) =>
        {
            var (ok, error) = await svc.DeactivateAsync(userId, ct);
            return ok ? Results.NoContent() :
                error == "not_found" ? Results.NotFound() : Results.BadRequest(new { error });
        });

        tg.MapPost("{userId}/reset-password", async (string userId, StudentService svc, CancellationToken ct) =>
        {
            var (ok, error, temp) = await svc.ResetPasswordAsync(userId, ct);
            return ok ? Results.Ok(new { tempPassword = temp }) :
                error == "not_found" ? Results.NotFound() : Results.BadRequest(new { error });
        });

        tg.MapDelete("{userId}", async (string userId, StudentService svc, CancellationToken ct) =>
        {
            var (ok, error) = await svc.DeleteAsync(userId, ct);
            return ok ? Results.NoContent() :
                error == "not_found" ? Results.NotFound() : Results.BadRequest(new { error });
        });

        return app;
    }

    public record CreateStudentRequest(
        [property: Required]
        [property: EmailAddress]
        string Email,
        string? Password,
        string? FirstName,
        string? LastName,
        IEnumerable<Guid>? Classes);

    public record UpdateStudentRequest(
        string? FirstName,
        string? LastName,
        IEnumerable<Guid>? ClassesAdd,
        IEnumerable<Guid>? ClassesRemove);
}