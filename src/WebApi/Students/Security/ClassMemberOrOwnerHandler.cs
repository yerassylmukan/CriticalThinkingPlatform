using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using WebApi.Auth.Infrastructure.Data;

namespace WebApi.Students.Security;

public sealed class ClassMemberOrOwnerHandler : AuthorizationHandler<ClassMemberOrOwnerRequirement>
{
    private readonly SchoolDbContext _db;

    public ClassMemberOrOwnerHandler(SchoolDbContext db)
    {
        _db = db;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ClassMemberOrOwnerRequirement requirement)
    {
        if (context.Resource is not HttpContext http)
            return;

        var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return;

        var classIdStr = http.Request.RouteValues.TryGetValue("classId", out var v) ? v?.ToString() : null;
        if (!Guid.TryParse(classIdStr, out var classId))
            return;

        var isOwner = await _db.Classes
            .AsNoTracking()
            .AnyAsync(c => c.Id == classId && c.OwnerTeacherId == userId);

        if (isOwner)
        {
            context.Succeed(requirement);
            return;
        }

        var isMember = await _db.ClassMembers
            .AsNoTracking()
            .AnyAsync(m => m.ClassId == classId && m.UserId == userId);

        if (isMember)
            context.Succeed(requirement);
    }
}