using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApi.Rag.Api;
using WebApi.Rag.Infrastructure.Data;
using WebApi.Rag.Infrastructure.Ollama;
using WebApi.Rag.Security;

namespace WebApi.Rag;

public static class RagModule
{
    public static void AddRagInfra(this WebApplicationBuilder b)
    {
        b.Services.AddDbContext<RagDbContext>(opt =>
            opt.UseNpgsql(
                b.Configuration.GetConnectionString("Default"),
                npg =>
                {
                    npg.UseVector();
                }));

        b.Services.AddHttpClient<OllamaClient>((sp, http) =>
        {
            var cfg = sp.GetRequiredService<IOptions<WebApi.Common.AppSettings>>().Value;
            var baseUrl = cfg.Ollama.BaseUrl?.TrimEnd('/') + "/";
            http.BaseAddress = new Uri(baseUrl ?? "http://localhost:11434/");
            http.Timeout = TimeSpan.FromSeconds(120);
        });

        b.Services.AddScoped<Application.RagService>();
        b.Services.AddScoped<Application.EvaluateService>();

        b.Services.AddAuthorization(o =>
        {
            o.AddPolicy("RagSessionOwner", p => p.AddRequirements(new SessionOwnerRequirement()));
            o.AddPolicy("RagTeacherOverStudent", p => p.RequireRole("Teacher").AddRequirements(new TeacherOverStudentRequirement()));
        });

        b.Services.AddScoped<IAuthorizationHandler, SessionOwnerHandler>();
        b.Services.AddScoped<IAuthorizationHandler, TeacherOverStudentHandler>();
    }

    public static IEndpointRouteBuilder MapRag(this IEndpointRouteBuilder app)
    {
        app.MapRagStudent();
        app.MapRagTeacher();
        return app;
    }
}
