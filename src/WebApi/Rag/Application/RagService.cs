using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using WebApi.Common;
using WebApi.Rag.Entities;
using WebApi.Rag.Enums;
using WebApi.Rag.Infrastructure.Data;
using WebApi.Rag.Infrastructure.OpenRouter;

namespace WebApi.Rag.Application;

public sealed class RagService
{
    private readonly RagDbContext _db;
    private readonly OpenRouterClient _llm;

    public RagService(RagDbContext db, OpenRouterClient llm)
    {
        _db = db;
        _llm = llm;
    }

    public async Task<Topic> CreateTopicWithGeneratedAnswersAsync(
        string title,
        IEnumerable<string> questions,
        string? conspect = null,
        string lang = "English",
        string? teacherId = null,
        CancellationToken ct = default)
    {
        var questionList = questions.ToList();

        var topic = new Topic
        {
            Title = title,
            CreatedUtc = DateTime.UtcNow,
            Conspect = conspect,
            TeacherId = teacherId
        };

        foreach (var q in questionList.Select(qtext => new Question { Text = qtext }))
        {
            topic.Questions.Add(q);
        }

        var prompt = PromptTemplates.BuildGenerationPromptBatch(questionList, lang);
        var json = await _llm.ChatAsync(prompt, jsonResponse: true, ct);

        var parsed = JsonSerializer.Deserialize<BatchGeneratedAnswerResponse>(json)
                     ?? throw new InvalidOperationException(
                         $"Failed to parse batch generated answers JSON. Raw: {json}");

        if (parsed.items == null || parsed.items.Count == 0)
            throw new InvalidOperationException(
                $"LLM returned no items for batch generation. Raw: {json}");

        foreach (var item in parsed.items)
        {
            if (item.index < 0 || item.index >= topic.Questions.Count)
                throw new InvalidOperationException(
                    $"LLM returned invalid question index {item.index}. Raw: {json}");

            var questionEntity = topic.Questions[item.index];

            if (item.answers == null || item.answers.Count == 0)
                throw new InvalidOperationException(
                    $"LLM returned no answers for question index {item.index}. Raw: {json}");

            foreach (var a in item.answers)
            {
                var text = a.text?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                var levelRaw = a.level?.Trim().ToLowerInvariant();
                var level = levelRaw switch
                {
                    "low" => AnswerLevel.Low,
                    "medium" => AnswerLevel.Medium,
                    "high" => AnswerLevel.High,
                    _ => AnswerLevel.Low
                };

                questionEntity.Generated.Add(new GeneratedAnswer
                {
                    Level = level,
                    Text = text
                });
            }

            if (questionEntity.Generated.Count == 0)
                throw new InvalidOperationException(
                    $"LLM returned only empty/invalid answers for question index {item.index}. Raw: {json}");
        }

        _db.Topics.Add(topic);
        await _db.SaveChangesAsync(ct);

        return topic;
    }

    public async Task<Topic> CreateTopicWithGeneratedAnswersAndConspectAsync(
        string title,
        IEnumerable<string> questions,
        string lang = "English",
        string? teacherId = null,
        CancellationToken ct = default)
    {
        var topic = await CreateTopicWithGeneratedAnswersAsync(title, questions, null, lang, teacherId, ct);

        var conspectPrompt = PromptTemplates.BuildConspectPrompt(title, questions, lang);
        var conspect = await _llm.ChatAsync(conspectPrompt, false, ct);

        topic.Conspect = conspect;
        await _db.SaveChangesAsync(ct);

        return topic;
    }

    public async Task<Guid> CreateSessionAsync(Guid topicId, string studentId, CancellationToken ct = default)
    {
        var topicExists = await _db.Topics.AnyAsync(t => t.Id == topicId, ct);
        if (!topicExists) throw new KeyNotFoundException("Topic not found.");

        var session = new StudentSession
        {
            TopicId = topicId,
            StudentId = studentId,
            StartedUtc = DateTime.UtcNow
        };

        _db.StudentSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        return session.Id;
    }

    private sealed class GeneratedAnswerResponse
    {
        public List<AnswerItem> answers { get; set; } = new();

        public sealed class AnswerItem
        {
            public string level { get; set; } = default!;
            public int score { get; set; }
            public string text { get; set; } = default!;
        }
    }
    
    private sealed class BatchGeneratedAnswerResponse
    {
        public List<QuestionAnswers> items { get; set; } = new();

        public sealed class QuestionAnswers
        {
            public int index { get; set; }
            public string question { get; set; } = default!;
            public List<AnswerItem> answers { get; set; } = new();
        }

        public sealed class AnswerItem
        {
            public string level { get; set; } = default!;
            public int score { get; set; }
            public string text { get; set; } = default!;
        }
    }
}