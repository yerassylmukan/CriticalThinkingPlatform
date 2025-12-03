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

            var studentIdsQuery = from cm in school.ClassMembers
                where teacherClassIds.Contains(cm.ClassId)
                select cm.UserId;

            var studentIds = await studentIdsQuery.Distinct().ToListAsync();
            var totalStudents = studentIds.Count;

            var totalSessions = studentIds.Count > 0
                ? await rag.StudentSessions.CountAsync(s => studentIds.Contains(s.StudentId))
                : 0;

            var totalTopics = await rag.Topics.CountAsync(t => t.TeacherId == tid);

            var newStudents = teacherClassIds.Count > 0
                ? await (from cm in school.ClassMembers
                    where teacherClassIds.Contains(cm.ClassId) && cm.JoinedUtc >= sevenDaysAgo
                    select cm.UserId).Distinct().CountAsync()
                : 0;

            var completedSessions = studentIds.Count > 0
                ? await (from s in rag.StudentSessions
                    join e in rag.Evaluations on s.Id equals e.SessionId
                    where studentIds.Contains(s.StudentId) && s.StartedUtc >= sevenDaysAgo
                    select s.Id).Distinct().CountAsync()
                : 0;

            var activeStudentIds = studentIds.Count > 0
                ? await (from s in rag.StudentSessions
                    where studentIds.Contains(s.StudentId) && s.StartedUtc >= sevenDaysAgo
                    select s.StudentId).Distinct().ToListAsync()
                : new List<string>();

            var activeClassIds = activeStudentIds.Count > 0 && teacherClassIds.Count > 0
                ? await (from cm in school.ClassMembers
                    where activeStudentIds.Contains(cm.UserId) && teacherClassIds.Contains(cm.ClassId)
                    select cm.ClassId).Distinct().CountAsync()
                : 0;

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

            if (teacherClassIds.Count == 0)
                return Results.Ok(new List<object>());

            var studentIds = await (from cm in school.ClassMembers
                where teacherClassIds.Contains(cm.ClassId)
                select cm.UserId).Distinct().ToListAsync();

            if (studentIds.Count == 0)
                return Results.Ok(new List<object>());

            var studentScores = await (from s in rag.StudentSessions
                join e in rag.Evaluations on s.Id equals e.SessionId
                where studentIds.Contains(s.StudentId)
                group e by s.StudentId into g
                select new
                {
                    StudentId = g.Key,
                    AvgScore = g.Average(e => e.TotalScore)
                })
                .OrderByDescending(x => x.AvgScore)
                .Take(10)
                .ToListAsync();

            if (studentScores.Count == 0)
                return Results.Ok(new List<object>());

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

