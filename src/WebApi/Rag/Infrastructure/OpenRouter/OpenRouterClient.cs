using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WebApi.Common;

namespace WebApi.Rag.Infrastructure.OpenRouter;

public sealed class OpenRouterClient
{
    private readonly HttpClient _http;
    private readonly AppSettings _cfg;

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
        {
            throw new InvalidOperationException(
                $"OpenRouter HTTP error {(int)resp.StatusCode}: {resp.StatusCode}. Body: {rawBody}");
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            if (!root.TryGetProperty("choices", out var choices) ||
                choices.ValueKind != JsonValueKind.Array ||
                choices.GetArrayLength() == 0)
            {
                throw new InvalidOperationException(
                    $"OpenRouter returned no choices. Raw body: {rawBody}");
            }

            var first = choices[0];

            string? content = null;
            
            if (first.TryGetProperty("message", out var msgEl))
            {
                if (msgEl.ValueKind == JsonValueKind.Object &&
                    msgEl.TryGetProperty("content", out var contEl) &&
                    contEl.ValueKind == JsonValueKind.String)
                {
                    content = contEl.GetString();
                }
                else if (msgEl.ValueKind == JsonValueKind.String)
                {
                    content = msgEl.GetString();
                }
            }
            
            if (string.IsNullOrWhiteSpace(content) &&
                first.TryGetProperty("text", out var textEl) &&
                textEl.ValueKind == JsonValueKind.String)
            {
                content = textEl.GetString();
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException(
                    $"OpenRouter returned empty content. Raw body: {rawBody}");
            }

            return content;
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse OpenRouter response. Raw body: {rawBody}", ex);
        }
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
        {
            throw new InvalidOperationException(
                $"OpenRouter embedding HTTP error {(int)resp.StatusCode}: {resp.StatusCode}. Body: {rawBody}");
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            if (!root.TryGetProperty("data", out var dataEl) ||
                dataEl.ValueKind != JsonValueKind.Array ||
                dataEl.GetArrayLength() == 0)
            {
                throw new InvalidOperationException(
                    $"OpenRouter returned no embedding data. Raw body: {rawBody}");
            }

            var first = dataEl[0];

            if (!first.TryGetProperty("embedding", out var embEl) ||
                embEl.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException(
                    $"OpenRouter returned invalid embedding format. Raw body: {rawBody}");
            }

            var list = new List<float>();
            foreach (var v in embEl.EnumerateArray())
            {
                if (v.ValueKind == JsonValueKind.Number && v.TryGetSingle(out var f))
                    list.Add(f);
            }

            if (list.Count == 0)
            {
                throw new InvalidOperationException(
                    $"OpenRouter returned empty embedding array. Raw body: {rawBody}");
            }

            return list.ToArray();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse OpenRouter embedding response. Raw body: {rawBody}", ex);
        }
    }
}
