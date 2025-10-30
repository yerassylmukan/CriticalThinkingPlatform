using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApi.Common;
using WebApi.Rag.Entities;
using WebApi.Rag.Infrastructure.Data;
using WebApi.Rag.Infrastructure.Ollama;

namespace WebApi.Rag.Application;

public class EvaluateService(
    RagDbContext db,
    OllamaClient ollama,
    IOptions<AppSettings> opts)
{
    private readonly string _model = opts.Value.Llm.Model;

    public async Task<Guid> EvaluateAsync(Guid sessionId, CancellationToken ct = default)
    {
        var evaluatedData = await db.Evaluations.AsNoTracking().SingleOrDefaultAsync(x => x.SessionId == sessionId, ct);

        if (evaluatedData != null) return evaluatedData.Id;

        var session = await db.StudentSessions
            .Include(s => s.Topic).ThenInclude(t => t.Questions).ThenInclude(q => q.Generated)
            .Include(s => s.Responses)
            .SingleAsync(s => s.Id == sessionId, ct);

        var perQuestion = new List<object>();
        var scores = new List<decimal>();

        foreach (var r in session.Responses)
        {
            var q = session.Topic.Questions.Single(x => x.Id == r.QuestionId);

            var golds = q.Generated
                .OrderByDescending(g => g.Level)
                .Select(g => (g.Level.ToString().ToLower(), (int)g.Level, g.Text))
                .ToList();

            var prompt = PromptTemplates.BuildEvaluationPrompt(q.Text, r.Answer, golds);

            var el = await ollama.GenerateJsonAsync(_model, prompt, 0);

            var baseScore = 0;
            var adjustment = 0;

            if (el.TryGetProperty("match_level", out var ml))
                switch ((ml.GetString() ?? "").ToLowerInvariant())
                {
                    case "low": baseScore = 50; break;
                    case "medium": baseScore = 75; break;
                    case "high": baseScore = 100; break;
                }

            if (el.TryGetProperty("base", out var b) && b.ValueKind == JsonValueKind.Number)
            {
                var bval = b.TryGetInt32(out var bi) ? bi : (int)Math.Round(b.GetDouble());
                if (Math.Abs(bval - 50) <= 2) baseScore = 50;
                else if (Math.Abs(bval - 75) <= 2) baseScore = 75;
                else if (Math.Abs(bval - 100) <= 2) baseScore = 100;
            }

            if (el.TryGetProperty("adjustment", out var adj) && adj.ValueKind == JsonValueKind.Number)
            {
                adjustment = adj.TryGetInt32(out var ai) ? ai : (int)Math.Round(adj.GetDouble());
                adjustment = Math.Max(-5, Math.Min(5, adjustment));
            }

            var fallback = 0;
            if (el.TryGetProperty("score", out var s))
            {
                if (s.ValueKind == JsonValueKind.Number)
                    fallback = s.TryGetInt32(out var iv) ? iv : (int)Math.Round(s.GetDouble());
                else if (s.ValueKind == JsonValueKind.String && int.TryParse(s.GetString(), out var sv))
                    fallback = sv;
            }

            if (baseScore == 0)
            {
                if (fallback == 0) fallback = 60;
                var diffs = new[]
                    { (50, Math.Abs(fallback - 50)), (75, Math.Abs(fallback - 75)), (100, Math.Abs(fallback - 100)) };
                baseScore = diffs.OrderBy(d => d.Item2).First().Item1;
                adjustment = Math.Max(-5, Math.Min(5, fallback - baseScore));
            }

            var finalScore = Math.Max(0, Math.Min(100, baseScore + adjustment));
            scores.Add(finalScore);

            var pq = new
            {
                questionId = q.Id,
                match_level = el.TryGetProperty("match_level", out var _ml) ? _ml.GetString() ?? "" :
                    baseScore == 50 ? "low" :
                    baseScore == 75 ? "medium" : "high",
                baseScoreValue = baseScore,
                adjustment,
                score = finalScore,
                rationale = el.TryGetProperty("rationale", out var rj) ? rj.GetString() ?? "" : "",
                strengths = el.TryGetProperty("strengths", out var st) && st.ValueKind == JsonValueKind.Array
                    ? st.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToArray()
                    : [],
                recommendations = el.TryGetProperty("recommendations", out var rc) &&
                                  rc.ValueKind == JsonValueKind.Array
                    ? rc.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x.Length > 0).ToArray()
                    : [],
                advice = el.TryGetProperty("advice", out var ad) ? ad.GetString() ?? "" : ""
            };
            perQuestion.Add(pq);
        }

        var overall = scores.Count == 0
            ? 0m
            : Math.Round(scores.Average(), 2, MidpointRounding.AwayFromZero);

        var payload = new
        {
            sessionId,
            overallScore = overall,
            perQuestion
        };

        var ev = new Evaluation
        {
            SessionId = sessionId,
            TotalScore = overall,
            ReportJson = JsonSerializer.Serialize(payload)
        };

        db.Evaluations.Add(ev);
        await db.SaveChangesAsync(ct);
        return ev.Id;
    }

    public async Task<string?> GetReportBySessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await db.Evaluations
            .AsNoTracking()
            .Where(x => x.SessionId == sessionId)
            .Select(x => x.ReportJson)
            .SingleOrDefaultAsync(ct);
    }

    public Task<Evaluation?> GetEvaluationAsync(Guid evaluationId, CancellationToken ct = default)
    {
        return db.Evaluations.AsNoTracking().SingleOrDefaultAsync(x => x.Id == evaluationId, ct);
    }
}