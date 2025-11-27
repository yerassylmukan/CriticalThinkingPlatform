using System.Text.Json;
using Microsoft.Extensions.Options;
using WebApi.Common;

namespace WebApi.Rag.Infrastructure.OpenRouter;

public sealed class OpenRouterClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly AppSettings _cfg;
    private readonly HttpClient _http;

    public OpenRouterClient(HttpClient http, IOptions<AppSettings> cfg)
    {
        _http = http;
        _cfg = cfg.Value;
    }

    public async Task<string> ChatAsync(
        string prompt,
        bool jsonResponse = false,
        CancellationToken ct = default)
    {
        var model = _cfg.Llm.Model;

        var body = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = "You are a helpful assistant." },
                new { role = "user", content = prompt }
            },
            response_format = jsonResponse ? new { type = "json_object" } : null
        };

        using var resp = await _http.PostAsJsonAsync("chat/completions", body, ct);
        var rawBody = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"OpenRouter HTTP error {(int)resp.StatusCode}: {resp.StatusCode}. Body: {rawBody}");

        ChatResponse? doc;
        try
        {
            doc = JsonSerializer.Deserialize<ChatResponse>(rawBody, JsonOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize OpenRouter response. Raw body: {rawBody}", ex);
        }

        if (doc?.Choices == null || doc.Choices.Count == 0)
            throw new InvalidOperationException(
                $"OpenRouter returned no choices. Raw body: {rawBody}");

        var first = doc.Choices[0];

        var msg = first.Message?.Content;
        if (string.IsNullOrWhiteSpace(msg))
            msg = first.Text;

        if (string.IsNullOrWhiteSpace(msg))
            throw new InvalidOperationException(
                $"OpenRouter returned empty content. Raw body: {rawBody}");

        return msg;
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var model = _cfg.Embed.Model;

        var body = new
        {
            model,
            input = text
        };

        using var resp = await _http.PostAsJsonAsync("embeddings", body, ct);
        var rawBody = await resp.Content.ReadAsStringAsync(ct);

        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"OpenRouter embedding HTTP error {(int)resp.StatusCode}: {resp.StatusCode}. Body: {rawBody}");

        EmbeddingResponse? doc;
        try
        {
            doc = JsonSerializer.Deserialize<EmbeddingResponse>(rawBody, JsonOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to deserialize OpenRouter embedding response. Raw body: {rawBody}", ex);
        }

        var emb = doc?.Data?.FirstOrDefault()?.Embedding;
        if (emb is null || emb.Length == 0)
            throw new InvalidOperationException(
                $"OpenRouter returned empty embedding. Raw body: {rawBody}");

        return emb;
    }

    private sealed class ChatResponse
    {
        public List<Choice> Choices { get; set; } = new();
    }

    private sealed class Choice
    {
        public Message? Message { get; set; }

        public string? Text { get; set; }
    }

    private sealed class Message
    {
        public string Role { get; set; } = default!;
        public string Content { get; } = default!;
    }

    private sealed class EmbeddingResponse
    {
        public List<EmbeddingData> Data { get; set; } = new();
    }

    private sealed class EmbeddingData
    {
        public float[] Embedding { get; } = Array.Empty<float>();
    }
}