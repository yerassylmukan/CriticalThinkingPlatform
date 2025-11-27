namespace WebApi.Common;

public class AppSettings
{
    public LlmSettings Llm { get; set; } = new();
    public EmbedSettings Embed { get; set; } = new();
    public OpenRouterSettings OpenRouter { get; set; } = new();
}

public class LlmSettings
{
    public string Model { get; set; } = "x-ai/grok-4.1-fast:free";
}

public class EmbedSettings
{
    public string Model { get; set; } = "voyage-lite-02-instruct";
}

public class OpenRouterSettings
{
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1";
    public string ApiKey { get; set; } = string.Empty;
    public string? Title { get; set; }
}