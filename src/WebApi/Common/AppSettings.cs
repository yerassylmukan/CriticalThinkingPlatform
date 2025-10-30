namespace WebApi.Common;

public class AppSettings
{
    public OllamaSettings Ollama { get; set; } = new();
    public LlmSettings Llm { get; set; } = new();
    public EmbedSettings Embed { get; set; } = new();
}

public class OllamaSettings
{
    public string BaseUrl { get; set; } = "http://localhost:11434";
}

public class LlmSettings
{
    public string Model { get; set; } = "llama3.2:3b";
}

public class EmbedSettings
{
    public string Model { get; set; } = "nomic-embed-text";
}