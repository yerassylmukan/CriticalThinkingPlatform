using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Rag.Application;
using WebApi.Rag.Entities;
using WebApi.Rag.Infrastructure.Data;
using WebApi.Rag.Security;

namespace WebApi.Rag.Api;

public static class StudentRagEndpoints
{
    public static IEndpointRouteBuilder MapRagStudent(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/rag/student")
            .WithTags("RAG:STUDENT")
            .RequireAuthorization(p => p.RequireRole("Student"));

        g.MapGet("/topics", async (RagDbContext db, [FromQuery] int page, [FromQuery] int pageSize) =>
        {
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 20 : Math.Min(100, pageSize);

            var total = await db.Topics.CountAsync();
            var items = await db.Topics
                .OrderByDescending(t => t.CreatedUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new { t.Id, t.Title, t.CreatedUtc })
                .ToListAsync();

            return Results.Ok(new { items, total, page, pageSize });
        });

        g.MapGet("/topics/{topicId:guid}", async (Guid topicId, RagDbContext db) =>
        {
            var t = await db.Topics
                .Include(t => t.Questions).ThenInclude(q => q.Generated)
                .AsNoTracking()
                .SingleOrDefaultAsync(t => t.Id == topicId);
            return t is null ? Results.NotFound() : Results.Ok(t);
        });

        g.MapPost("/sessions", async ([FromBody] CreateStudentSessionRequest r, ClaimsPrincipal user, RagService svc) =>
        {
            var uid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var id = await svc.CreateSessionAsync(r.TopicId, uid);
            return Results.Created($"/rag/student/sessions/{id}", new { id });
        });

        g.MapGet("/sessions", async (ClaimsPrincipal user, RagDbContext db, [FromQuery] int page, [FromQuery] int pageSize) =>
        {
            var uid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 20 : Math.Min(100, pageSize);

            var q = db.StudentSessions
                .Include(s => s.Evaluation)
                .Include(s => s.Topic)
                .Where(s => s.StudentId == uid);

            var total = await q.CountAsync();
            var items = await q
                .OrderByDescending(s => s.StartedUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(s => new StudentSessionListItem(
                    s.Id, s.TopicId, s.Topic!.Title, s.StartedUtc, s.Evaluation != null, s.Evaluation!.TotalScore))
                .ToListAsync();

            return Results.Ok(new { items, total, page, pageSize });
        });

        g.MapGet("/sessions/{id:guid}", async (Guid id, RagDbContext db) =>
        {
            var s = await db.StudentSessions
                .Include(s => s.Topic).ThenInclude(t => t.Questions).ThenInclude(q => q.Generated)
                .Include(s => s.Responses)
                .Include(s => s.Evaluation)
                .AsNoTracking()
                .SingleOrDefaultAsync(s => s.Id == id);
            return s is null ? Results.NotFound() : Results.Ok(s);
        }).RequireAuthorization(p => p.AddRequirements(new SessionOwnerRequirement()));

        g.MapPost("/sessions/{id:guid}/submit", async (Guid id, [FromBody] SubmitAnswersRequest r, RagDbContext db) =>
        {
            var session = await db.StudentSessions.Include(s => s.Topic).ThenInclude(t => t.Questions)
                .SingleAsync(s => s.Id == id);

            var existed = await db.StudentResponses.Where(x => x.SessionId == id).ToListAsync();
            if (existed.Count > 0) db.StudentResponses.RemoveRange(existed);

            foreach (var a in r.Answers)
                db.StudentResponses.Add(new StudentResponse
                    { SessionId = id, QuestionId = a.QuestionId, Answer = a.Answer });

            await db.SaveChangesAsync();
            return Results.Accepted();
        }).RequireAuthorization(p => p.AddRequirements(new SessionOwnerRequirement()));

        g.MapPost("/sessions/{id:guid}/evaluate", async (Guid id, EvaluateService svc) =>
        {
            var evaluationId = await svc.EvaluateAsync(id);
            return Results.Created($"/rag/student/evaluations/{evaluationId}", null);
        }).RequireAuthorization(p => p.AddRequirements(new SessionOwnerRequirement()));

        g.MapGet("/sessions/{id:guid}/report", async (Guid id, EvaluateService svc) =>
        {
            var json = await svc.GetReportBySessionAsync(id);
            return json is null ? Results.NotFound() : Results.Content(json, "application/json");
        }).RequireAuthorization(p => p.AddRequirements(new SessionOwnerRequirement()));

        return app;
    }

    public record CreateStudentSessionRequest([property: Required] Guid TopicId);

    public record StudentSessionListItem(Guid Id, Guid TopicId, string TopicTitle, DateTime StartedUtc, bool Evaluated, decimal? TotalScore);
}
