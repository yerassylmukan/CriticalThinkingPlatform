using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using WebApi.Common;
using WebApi.Rag.Api;
using WebApi.Rag.Application;
using WebApi.Rag.Infrastructure.Data;
using WebApi.Rag.Infrastructure.OpenRouter;
using WebApi.Rag.Security;

namespace WebApi.Rag;

public static class RagModule
{
    public static void AddRagInfra(this WebApplicationBuilder b)
    {
        b.Services.AddDbContext<RagDbContext>(opt =>
            opt.UseNpgsql(
                b.Configuration.GetConnectionString("Default"),
                npg => { npg.UseVector(); }));

        b.Services.AddHttpClient<OpenRouterClient>((sp, http) =>
        {
            var cfg = sp.GetRequiredService<IOptions<AppSettings>>().Value;
            var or = cfg.OpenRouter;

            var baseUrl = (or.BaseUrl ?? "https://openrouter.ai/api/v1").TrimEnd('/') + "/";
            http.BaseAddress = new Uri(baseUrl);
            http.Timeout = TimeSpan.FromSeconds(120);

            if (!string.IsNullOrWhiteSpace(or.ApiKey))
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", or.ApiKey);

            if (!string.IsNullOrWhiteSpace(or.Title))
                http.DefaultRequestHeaders.Add("X-Title", or.Title);
        });

        b.Services.AddScoped<RagService>();
        b.Services.AddScoped<EvaluateService>();

        b.Services.AddAuthorization(o =>
        {
            o.AddPolicy("RagSessionOwner", p => p.AddRequirements(new SessionOwnerRequirement()));
            o.AddPolicy("RagTeacherOverStudent",
                p => p.RequireRole("Teacher").AddRequirements(new TeacherOverStudentRequirement()));
        });

        b.Services.AddScoped<IAuthorizationHandler, SessionOwnerHandler>();
        b.Services.AddScoped<IAuthorizationHandler, TeacherOverStudentHandler>();
    }

    public static IEndpointRouteBuilder MapRag(this IEndpointRouteBuilder app)
    {
        app.MapRagStudent();
        app.MapRagTeacher();
        app.MapDashboard();
        return app;
    }
}