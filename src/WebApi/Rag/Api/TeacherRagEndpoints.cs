using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Auth.Infrastructure.Data;
using WebApi.Rag.Application;
using WebApi.Rag.Infrastructure.Data;
using WebApi.Rag.Security;

namespace WebApi.Rag.Api;

public static class TeacherRagEndpoints
{
    public static IEndpointRouteBuilder MapRagTeacher(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/rag/teacher")
            .WithTags("RAG:TEACHER")
            .RequireAuthorization(p => p.RequireRole("Teacher"));

        g.MapPost("/topics", async ([FromBody] CreateTopicRequest r, ClaimsPrincipal user, RagService svc) =>
        {
            try
            {
                var tid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
                var lang = string.IsNullOrWhiteSpace(r.Lang) ? "English" : r.Lang;

                var topic = r.GenerateConspect
                    ? await svc.CreateTopicWithGeneratedAnswersAndConspectAsync(r.Title, r.Questions, lang, tid)
                    : await svc.CreateTopicWithGeneratedAnswersAsync(r.Title, r.Questions, r.Conspect, lang, tid);

                return Results.Json(new { topic.Id, topic.Title, topic.CreatedUtc });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("OpenRouter HTTP error 401"))
            {
                return Results.Problem(
                    title: "OpenRouter Authentication Failed",
                    detail: "The OpenRouter API key is invalid or expired. Please check your configuration.",
                    statusCode: 503);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("OpenRouter"))
            {
                return Results.Problem(
                    title: "OpenRouter API Error",
                    detail: ex.Message,
                    statusCode: 503);
            }
        });

        g.MapGet("/topics", async (ClaimsPrincipal user, RagDbContext db, [FromQuery] int page, [FromQuery] int pageSize) =>
        {
            var tid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 20 : Math.Min(100, pageSize);

            var q = db.Topics.Where(t => t.TeacherId == tid);

            var total = await q.CountAsync();
            var items = await q
                .OrderByDescending(t => t.CreatedUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new { t.Id, t.Title, t.CreatedUtc })
                .ToListAsync();

            return Results.Ok(new { items, total, page, pageSize });
        });

        g.MapGet("/topics/{id:guid}", async (Guid id, RagDbContext db) =>
            await db.Topics.Include(t => t.Questions).ThenInclude(q => q.Generated)
                .AsNoTracking()
                .SingleOrDefaultAsync(t => t.Id == id) is { } t
                ? Results.Ok(t)
                : Results.NotFound());

        g.MapGet("/classes/{classId:guid}/sessions", async (Guid classId, ClaimsPrincipal user,
            RagDbContext rag, SchoolDbContext school, [FromQuery] int page, [FromQuery] int pageSize) =>
        {
            var tid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var own = await school.Classes.AnyAsync(c => c.Id == classId && c.OwnerTeacherId == tid);
            if (!own) return Results.Forbid();

            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 20 : Math.Min(100, pageSize);

            var studentIds = await school.ClassMembers
                .Where(m => m.ClassId == classId)
                .Select(m => m.UserId)
                .Distinct()
                .ToListAsync();

            var q = rag.StudentSessions
                .Include(s => s.Evaluation)
                .Include(s => s.Topic)
                .Where(s => studentIds.Contains(s.StudentId));

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(s => s.StartedUtc)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(s => new TeacherSessionListItem(
                    s.Id, s.StudentId, s.TopicId, s.Topic!.Title, s.StartedUtc,
                    s.Evaluation != null, s.Evaluation!.TotalScore))
                .ToListAsync();

            return Results.Ok(new { items, total, page, pageSize });
        });

        g.MapGet("/sessions", async (ClaimsPrincipal user,
            RagDbContext rag, SchoolDbContext school, [FromQuery] int page, [FromQuery] int pageSize) =>
        {
            var tid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;

            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 20 : Math.Min(100, pageSize);

            var teacherClassIds = await school.Classes
                .Where(c => c.OwnerTeacherId == tid)
                .Select(c => c.Id)
                .ToListAsync();

            var studentIds = await school.ClassMembers
                .Where(m => teacherClassIds.Contains(m.ClassId))
                .Select(m => m.UserId)
                .Distinct()
                .ToListAsync();

            var q = rag.StudentSessions
                .Include(s => s.Evaluation)
                .Include(s => s.Topic)
                .Where(s => studentIds.Contains(s.StudentId));

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(s => s.StartedUtc)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(s => new TeacherSessionListItem(
                    s.Id, s.StudentId, s.TopicId, s.Topic!.Title, s.StartedUtc,
                    s.Evaluation != null, s.Evaluation!.TotalScore))
                .ToListAsync();

            return Results.Ok(new { items, total, page, pageSize });
        });

        g.MapGet("/students/{studentId}/sessions", async (string studentId, ClaimsPrincipal user,
            RagDbContext rag, SchoolDbContext school, [FromQuery] int page, [FromQuery] int pageSize) =>
        {
            var tid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var anyRelation = await (from c in school.Classes
                join m in school.ClassMembers on c.Id equals m.ClassId
                where c.OwnerTeacherId == tid && m.UserId == studentId
                select c.Id).AnyAsync();

            if (!anyRelation) return Results.Forbid();

            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 20 : Math.Min(100, pageSize);

            var q = rag.StudentSessions
                .Include(s => s.Evaluation)
                .Include(s => s.Topic)
                .Where(s => s.StudentId == studentId);

            var total = await q.CountAsync();
            var items = await q.OrderByDescending(s => s.StartedUtc)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(s => new TeacherSessionListItem(
                    s.Id, s.StudentId, s.TopicId, s.Topic!.Title, s.StartedUtc,
                    s.Evaluation != null, s.Evaluation!.TotalScore))
                .ToListAsync();

            return Results.Ok(new { items, total, page, pageSize });
        });

        g.MapGet("/sessions/{id:guid}", async (Guid id, RagDbContext rag) =>
        {
            var s = await rag.StudentSessions
                .Include(s => s.Topic).ThenInclude(t => t.Questions).ThenInclude(q => q.Generated)
                .Include(s => s.Responses)
                .Include(s => s.Evaluation)
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == id);

            return s is null ? Results.NotFound() : Results.Ok(s);
        }).RequireAuthorization(p => p.AddRequirements(new TeacherOverStudentRequirement()));

        g.MapGet("/sessions/{id:guid}/report", async (Guid id, EvaluateService svc) =>
        {
            var json = await svc.GetReportBySessionAsync(id);
            return json is null ? Results.NotFound() : Results.Content(json, "application/json");
        }).RequireAuthorization(p => p.AddRequirements(new TeacherOverStudentRequirement()));

        g.MapPost("/sessions/{id:guid}/evaluate", async (Guid id, EvaluateService svc) =>
        {
            var evaluationId = await svc.EvaluateAsync(id);
            return Results.Created($"/rag/teacher/evaluations/{evaluationId}", null);
        }).RequireAuthorization(p => p.AddRequirements(new TeacherOverStudentRequirement()));

        return app;
    }

    public record TeacherSessionListItem(
        Guid Id,
        string StudentId,
        Guid TopicId,
        string TopicTitle,
        DateTime StartedUtc,
        bool Evaluated,
        decimal? TotalScore);
}