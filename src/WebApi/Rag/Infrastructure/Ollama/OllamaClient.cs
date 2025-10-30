using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WebApi.Common;

namespace WebApi.Rag.Infrastructure.Ollama;

public class OllamaClient
{
    private readonly HttpClient _http;

    public OllamaClient(IOptions<AppSettings> opts, HttpClient http)
    {
        _http = http;
        _http.Timeout = TimeSpan.FromSeconds(120);
    }

    public async Task<string> GenerateAsync(string model, string prompt, int? temperature = null,
        CancellationToken ct = default)
    {
        var req = new
        {
            model,
            prompt,
            stream = false,
            options = temperature.HasValue ? new { temperature } : null
        };

        using var resp = await _http.PostAsJsonAsync("api/generate", req, ct);
        resp.EnsureSuccessStatusCode();

        var outer = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        return outer.GetProperty("response").GetString() ?? string.Empty;
    }

    public async Task<JsonElement> GenerateJsonAsync(string model, string prompt, int? temperature = null,
        CancellationToken ct = default)
    {
        var req = new
        {
            model,
            prompt,
            stream = false,
            format = "json",
            options = temperature.HasValue ? new { temperature } : null
        };

        using var resp = await _http.PostAsJsonAsync("api/generate", req, ct);
        if (resp.StatusCode == HttpStatusCode.NotFound)
        {
        }

        resp.EnsureSuccessStatusCode();

        var outer = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        var responseText = outer.GetProperty("response").GetString() ?? string.Empty;

        if (TryParseJson(responseText, out var root) ||
            (TryExtractJson(responseText, out var cleaned) && TryParseJson(cleaned, out root)))
            return root;

        throw new InvalidOperationException("LLM returned non-JSON response.");
    }

    public async Task<float[]> EmbedAsync(string model, string text, CancellationToken ct = default)
    {
        var req = new { model, input = text };
        using var resp = await _http.PostAsJsonAsync("api/embeddings", req, ct);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
        var arr = json.GetProperty("embedding").EnumerateArray().Select(e => (float)e.GetDouble()).ToArray();
        return arr;
    }

    private static bool TryParseJson(string s, out JsonElement root)
    {
        try
        {
            using var doc = JsonDocument.Parse(s);
            root = doc.RootElement.Clone();
            return true;
        }
        catch
        {
            root = default;
            return false;
        }
    }

    private static bool TryExtractJson(string text, out string json)
    {
        var i = text.IndexOf('{');
        var j = text.LastIndexOf('}');
        if (i >= 0 && j > i)
        {
            json = text.Substring(i, j - i + 1);
            return true;
        }

        json = string.Empty;
        return false;
    }
}