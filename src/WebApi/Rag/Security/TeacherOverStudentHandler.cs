using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using WebApi.Auth.Infrastructure.Data;
using WebApi.Rag.Infrastructure.Data;

namespace WebApi.Rag.Security;

public sealed class TeacherOverStudentHandler : AuthorizationHandler<TeacherOverStudentRequirement>
{
    private readonly SchoolDbContext _school;
    private readonly RagDbContext _rag;

    public TeacherOverStudentHandler(SchoolDbContext school, RagDbContext rag)
    {
        _school = school;
        _rag = rag;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, TeacherOverStudentRequirement requirement)
    {
        if (context.Resource is not HttpContext http) return;

        var teacherId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(teacherId)) return;

        string? studentId = null;

        var idStr = http.Request.RouteValues.TryGetValue("id", out var rid) ? rid?.ToString() : null;
        if (Guid.TryParse(idStr, out var sessionId))
        {
            studentId = await _rag.StudentSessions.AsNoTracking()
                .Where(s => s.Id == sessionId)
                .Select(s => s.StudentId)
                .SingleOrDefaultAsync();
        }

        if (string.IsNullOrEmpty(studentId))
        {
            if (http.Request.Query.TryGetValue("studentId", out var qval))
                studentId = qval.ToString();
        }

        if (string.IsNullOrEmpty(studentId)) return;

        var any = await (from c in _school.Classes
                         join m in _school.ClassMembers on c.Id equals m.ClassId
                         where c.OwnerTeacherId == teacherId && m.UserId == studentId
                         select c.Id).AnyAsync();

        if (any) context.Succeed(requirement);
    }
}
