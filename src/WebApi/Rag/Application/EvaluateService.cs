using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using WebApi.Common;
using WebApi.Rag.Entities;
using WebApi.Rag.Infrastructure.Data;
using WebApi.Rag.Infrastructure.OpenRouter;

namespace WebApi.Rag.Application;

public sealed class EvaluateService
{
    private readonly RagDbContext _db;
    private readonly OpenRouterClient _llm;

    public EvaluateService(RagDbContext db, OpenRouterClient llm)
    {
        _db = db;
        _llm = llm;
    }

    public async Task<Guid> EvaluateAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _db.StudentSessions
                          .Include(s => s.Topic)
                          .ThenInclude(t => t.Questions).ThenInclude(q => q.Generated)
                          .Include(s => s.Responses)
                          .SingleOrDefaultAsync(s => s.Id == sessionId, ct)
                      ?? throw new KeyNotFoundException("Session not found.");

        if (session.Evaluation != null)
            return session.Evaluation.Id;

        decimal sum = 0;
        var evaluatedCount = 0;
        var reportItems = new List<object>();

        foreach (var question in session.Topic.Questions)
        {
            var studentResponse = session.Responses
                .FirstOrDefault(r => r.QuestionId == question.Id);

            if (studentResponse == null)
                continue;

            var golds = question.Generated.Select(g => (
                level: g.Level.ToString().ToLower(),
                score: (int)g.Level,
                text: g.Text
            ));

            var prompt = PromptTemplates.BuildEvaluationPrompt(
                question.Text,
                studentResponse.Answer,
                golds
            );

            var json = await _llm.ChatAsync(prompt, true, ct);
            var parsed = JsonSerializer.Deserialize<EvalResult>(json)
                         ?? throw new InvalidOperationException("Failed to parse evaluation JSON.");

            sum += parsed.Score;
            evaluatedCount++;

            reportItems.Add(new
            {
                question = question.Text,
                student = studentResponse.Answer,
                match_level = parsed.MatchLevel,
                @base = parsed.BaseScore,
                adjustment = parsed.Adjustment,
                score = parsed.Score,
                rationale = parsed.Rationale,
                strengths = parsed.Strengths,
                recommendations = parsed.Recommendations,
                advice = parsed.Advice
            });
        }

        var final = evaluatedCount > 0 ? sum / evaluatedCount : 0;
        final = Math.Round(final, 2);

        var evaluation = new Evaluation
        {
            SessionId = sessionId,
            TotalScore = final,
            ReportJson = JsonSerializer.Serialize(reportItems)
        };

        _db.Evaluations.Add(evaluation);
        await _db.SaveChangesAsync(ct);

        return evaluation.Id;
    }

    public async Task<string?> GetReportBySessionAsync(Guid id)
    {
        return await _db.Evaluations
            .Where(e => e.SessionId == id)
            .Select(e => e.ReportJson)
            .SingleOrDefaultAsync();
    }

    private sealed class EvalResult
    {
        [JsonPropertyName("match_level")] public string MatchLevel { get; set; } = default!;

        [JsonPropertyName("base")] public int BaseScore { get; set; }

        [JsonPropertyName("adjustment")] public int Adjustment { get; set; }

        [JsonPropertyName("score")] public int Score { get; set; }

        [JsonPropertyName("rationale")] public string Rationale { get; set; } = default!;

        [JsonPropertyName("strengths")] public List<string> Strengths { get; set; } = new();

        [JsonPropertyName("recommendations")] public List<string> Recommendations { get; set; } = new();

        [JsonPropertyName("advice")] public string Advice { get; set; } = default!;
    }
}