using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Auth.Infrastructure.Data;
using WebApi.Rag.Infrastructure.Data;

namespace WebApi.Rag.Api;

public static class HomeEndpoints
{
    public static IEndpointRouteBuilder MapHome(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/v1/public")
            .WithTags("PUBLIC");

        g.MapGet("/statistics", async (RagDbContext rag, SchoolDbContext school) =>
        {
            var studentsCount = await school.StudentProfiles.CountAsync();
            var teachersCount = await school.TeacherProfiles.CountAsync();
            var topicsCount = await rag.Topics.CountAsync();

            var avgScore = await rag.Evaluations
                .Select(e => e.TotalScore)
                .DefaultIfEmpty(0)
                .AverageAsync();

            var successRate = Math.Round(avgScore, 0);

            return Results.Ok(new
            {
                studentsCount,
                teachersCount,
                topicsCount,
                successRate
            });
        });

        return app;
    }
}

