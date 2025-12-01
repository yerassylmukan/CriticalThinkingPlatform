using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApi.Auth.Entities;
using WebApi.Auth.Infrastructure.Data;
using WebApi.Rag.Infrastructure.Data;

namespace WebApi.Rag.Api;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboard(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/teacher/dashboard")
            .WithTags("DASHBOARD")
            .RequireAuthorization(p => p.RequireRole("Teacher"));

        g.MapGet("/overview", async (ClaimsPrincipal user, RagDbContext rag, SchoolDbContext school) =>
        {
            var tid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);

            var totalClasses = await school.Classes.CountAsync(c => c.OwnerTeacherId == tid);

            var teacherClassIds = await school.Classes
                .Where(c => c.OwnerTeacherId == tid)
                .Select(c => c.Id)
                .ToListAsync();

            var totalStudents = await school.ClassMembers
                .Where(m => teacherClassIds.Contains(m.ClassId))
                .Select(m => m.UserId)
                .Distinct()
                .CountAsync();

            var studentIds = await school.ClassMembers
                .Where(m => teacherClassIds.Contains(m.ClassId))
                .Select(m => m.UserId)
                .Distinct()
                .ToListAsync();

            var totalSessions = await rag.StudentSessions
                .CountAsync(s => studentIds.Contains(s.StudentId));

            var totalTopics = await rag.Topics.CountAsync(t => t.TeacherId == tid);

            var newStudents = await school.ClassMembers
                .Where(m => teacherClassIds.Contains(m.ClassId) && m.JoinedUtc >= sevenDaysAgo)
                .Select(m => m.UserId)
                .Distinct()
                .CountAsync();

            var completedSessions = await rag.StudentSessions
                .Where(s => studentIds.Contains(s.StudentId) && s.Evaluation != null && s.StartedUtc >= sevenDaysAgo)
                .CountAsync();

            var activeClassIds = await (from s in rag.StudentSessions
                join cm in school.ClassMembers on s.StudentId equals cm.UserId
                where studentIds.Contains(s.StudentId) && s.StartedUtc >= sevenDaysAgo && teacherClassIds.Contains(cm.ClassId)
                select cm.ClassId)
                .Distinct()
                .CountAsync();

            return Results.Ok(new
            {
                totalClasses,
                totalStudents,
                totalSessions,
                totalTopics,
                weeklyStats = new
                {
                    newStudents,
                    completedSessions,
                    activeClasses = activeClassIds
                }
            });
        });

        g.MapGet("/top-students", async (ClaimsPrincipal user, RagDbContext rag, SchoolDbContext school) =>
        {
            var tid = user.FindFirstValue(ClaimTypes.NameIdentifier)!;

            var teacherClassIds = await school.Classes
                .Where(c => c.OwnerTeacherId == tid)
                .Select(c => c.Id)
                .ToListAsync();

            var studentIds = await school.ClassMembers
                .Where(m => teacherClassIds.Contains(m.ClassId))
                .Select(m => m.UserId)
                .Distinct()
                .ToListAsync();

            var studentScores = await rag.StudentSessions
                .Include(s => s.Evaluation)
                .Where(s => studentIds.Contains(s.StudentId) && s.Evaluation != null)
                .GroupBy(s => s.StudentId)
                .Select(g => new
                {
                    StudentId = g.Key,
                    AvgScore = g.Average(s => s.Evaluation!.TotalScore)
                })
                .OrderByDescending(x => x.AvgScore)
                .Take(10)
                .ToListAsync();

            var userIds = studentScores.Select(x => x.StudentId).ToList();

            var students = await school.Users
                .Include(u => u.StudentProfile)
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();

            var classMemberships = await (from cm in school.ClassMembers
                join c in school.Classes on cm.ClassId equals c.Id
                where userIds.Contains(cm.UserId) && teacherClassIds.Contains(cm.ClassId)
                select new { cm.UserId, c.Name })
                .ToListAsync();

            var result = studentScores
                .Select(ss =>
                {
                    var student = students.FirstOrDefault(s => s.Id == ss.StudentId);
                    var profile = student?.StudentProfile;
                    var firstName = profile?.FirstName ?? "";
                    var lastName = profile?.LastName ?? "";
                    var name = $"{firstName} {lastName}".Trim();
                    if (string.IsNullOrEmpty(name)) name = student?.UserName ?? "";

                    var classMember = classMemberships.FirstOrDefault(cm => cm.UserId == ss.StudentId);
                    var className = classMember?.Name ?? "";

                    return new
                    {
                        id = ss.StudentId,
                        name,
                        @class = className,
                        score = Math.Round(ss.AvgScore, 2)
                    };
                })
                .ToList();

            return Results.Ok(result);
        });

        return app;
    }
}

