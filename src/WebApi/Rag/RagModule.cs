using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApi.Common;
using WebApi.Rag.Application;
using WebApi.Rag.Infrastructure.Data;
using WebApi.Rag.Infrastructure.Ollama;

namespace WebApi.Rag;

public static class RagModule
{
    public static void AddInfra(this WebApplicationBuilder b)
    {
        b.Services.AddDbContext<RagDbContext>(opt =>
            opt.UseNpgsql(
                b.Configuration.GetConnectionString("Default"),
                npg => npg.UseVector()
            )
        );

        b.Services.AddHttpClient<OllamaClient>((sp, http) =>
        {
            var cfg = sp.GetRequiredService<IOptions<AppSettings>>().Value;

            var baseUrl = cfg.Ollama.BaseUrl?.TrimEnd('/') + "/";
            http.BaseAddress = new Uri(baseUrl ?? "http://localhost:11434/");
            http.Timeout = TimeSpan.FromSeconds(120);
        });

        b.Services.AddScoped<RagService>();
        b.Services.AddScoped<EvaluateService>();
    }
}