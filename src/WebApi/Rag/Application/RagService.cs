using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;
using WebApi.Common;
using WebApi.Rag.Entities;
using WebApi.Rag.Enums;
using WebApi.Rag.Infrastructure.Data;
using WebApi.Rag.Infrastructure.Ollama;

namespace WebApi.Rag.Application;

public class RagService(
    RagDbContext db,
    OllamaClient ollama,
    IOptions<AppSettings> opts)
{
    private readonly string _embModel = opts.Value.Embed.Model;
    private readonly string _genModel = opts.Value.Llm.Model;

    public async Task<Topic> CreateTopicWithGeneratedAnswersAsync(
        string title,
        IEnumerable<string> questions,
        string? conspect
    )
    {
        var topic = new Topic { Title = title, Conspect = conspect };

        foreach (var q in questions)
        {
            var prompt = PromptTemplates.BuildGenerationPrompt(q);
            var root = await ollama.GenerateJsonAsync(_genModel, prompt, 0);

            if (!root.TryGetProperty("answers", out var answersArr) || answersArr.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Model response missing 'answers' array");

            var qq = new Question { Text = q };

            foreach (var it in answersArr.EnumerateArray())
            {
                var levelStr = it.TryGetProperty("level", out var l) ? l.GetString() ?? "low" : "low";
                var text = it.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";

                var level = levelStr.ToLowerInvariant() switch
                {
                    "low" => AnswerLevel.Low,
                    "medium" => AnswerLevel.Medium,
                    "high" => AnswerLevel.High,
                    _ => AnswerLevel.Low
                };

                qq.Generated.Add(new GeneratedAnswer { Level = level, Text = text });
            }

            topic.Questions.Add(qq);
        }

        db.Topics.Add(topic);
        await db.SaveChangesAsync();
        return topic;
    }

    public async Task<Topic> CreateTopicWithGeneratedAnswersAndConspectAsync(
        string title,
        IEnumerable<string> questions,
        string lang = "English")
    {
        var cprompt = PromptTemplates.BuildConspectPrompt(title, questions, lang);
        var conspect = await ollama.GenerateAsync(_genModel, cprompt, 0);

        return await CreateTopicWithGeneratedAnswersAsync(title, questions, conspect);
    }

    public async Task<Guid> CreateSessionAsync(Guid topicId, string studentId)
    {
        var alreadyCreatedSession = await db.StudentSessions.AsNoTracking()
            .SingleOrDefaultAsync(ss => ss.TopicId == topicId && ss.StudentId == studentId);

        if (alreadyCreatedSession != null) return alreadyCreatedSession.Id;

        var session = new StudentSession { TopicId = topicId, StudentId = studentId };
        db.StudentSessions.Add(session);
        await db.SaveChangesAsync();
        return session.Id;
    }

    public async Task<RagDocument> AddDocumentAsync(string content, string? source = null)
    {
        var floats = await ollama.EmbedAsync(_embModel, content);
        var doc = new RagDocument
        {
            Content = content,
            Source = source,
            Embedding = new Vector(floats)
        };

        db.RagDocuments.Add(doc);
        await db.SaveChangesAsync();
        return doc;
    }

    public async Task<List<RagDocument>> RetrieveAsync(string query, int k = 4)
    {
        var floats = await ollama.EmbedAsync(_embModel, query);
        var qv = new Vector(floats);

        const string sql = @"SELECT * FROM ""RagDocuments""
                             ORDER BY ""Embedding"" <-> @p
                             LIMIT @k";

        return await db.RagDocuments.FromSqlRaw(
            sql,
            new NpgsqlParameter("p", qv),
            new NpgsqlParameter("k", k <= 0 ? 4 : k)
        ).ToListAsync();
    }
}