using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Rag.Application;
using WebApi.Rag.Entities;
using WebApi.Rag.Infrastructure.Data;

namespace WebApi.Rag.Api;

public static class RagEndpoints
{
    public static void Map(RouteGroupBuilder g)
    {
        g.MapPost("/topics", async ([FromBody] CreateTopicRequest r, RagService svc) =>
        {
            var topic = r.GenerateConspect
                ? await svc.CreateTopicWithGeneratedAnswersAndConspectAsync(r.Title, r.Questions)
                : await svc.CreateTopicWithGeneratedAnswersAsync(r.Title, r.Questions, r.Conspect);

            return Results.Json(new { topic.Id, topic.Title, topic.CreatedUtc });
        });

        g.MapGet("/topics/{id:guid}", async (Guid id, RagDbContext db) =>
            await db.Topics.Include(t => t.Questions).ThenInclude(q => q.Generated)
                .SingleOrDefaultAsync(t => t.Id == id) is { } t
                ? Results.Ok(t)
                : Results.NotFound());

        g.MapPost("/sessions", async ([FromBody] CreateSessionRequest r, RagService svc) =>
        {
            var id = await svc.CreateSessionAsync(r.TopicId, r.StudentId);
            return Results.Created($"/sessions/{id}", new { id });
        });

        g.MapPost("/sessions/{id:guid}/submit", async (Guid id, [FromBody] SubmitAnswersRequest r, RagDbContext db) =>
        {
            var session = await db.StudentSessions.Include(s => s.Topic).ThenInclude(t => t.Questions)
                .SingleAsync(s => s.Id == id);
            foreach (var a in r.Answers)
                db.StudentResponses.Add(new StudentResponse
                    { SessionId = id, QuestionId = a.QuestionId, Answer = a.Answer });
            await db.SaveChangesAsync();
            return Results.Accepted();
        });

        g.MapPost("/sessions/{id:guid}/evaluate", async (Guid id, EvaluateService svc) =>
        {
            var evaluationId = await svc.EvaluateAsync(id);

            return Results.Created($"/evaluations/{evaluationId}", null);
        });

        g.MapGet("/sessions/{id:guid}/report", async (Guid id, EvaluateService svc) =>
        {
            var json = await svc.GetReportBySessionAsync(id);
            return json is null ? Results.NotFound() : Results.Content(json, "application/json");
        });

        g.MapGet("/evaluations/{evaluationId:guid}", async (Guid evaluationId, EvaluateService svc) =>
        {
            var ev = await svc.GetEvaluationAsync(evaluationId);
            return ev is null ? Results.NotFound() : Results.Content(ev.ReportJson, "application/json");
        });

        g.MapPost("/rag/docs",
            async ([FromBody] AddDocRequest r, RagService svc) =>
            Results.Ok(await svc.AddDocumentAsync(r.Content, r.Source)));
        g.MapGet("/rag/search",
            async ([FromQuery] string q, [FromQuery] int k, RagService svc) =>
            Results.Ok(await svc.RetrieveAsync(q, k <= 0 ? 4 : k)));
    }
}

public static class RagRouteMapping
{
    public static IEndpointRouteBuilder MapRag(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/rag").WithTags("RAG");
        RagEndpoints.Map(group);
        return app;
    }
}