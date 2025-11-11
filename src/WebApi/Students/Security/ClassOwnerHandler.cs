using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using WebApi.Auth.Infrastructure.Data;

namespace WebApi.Students.Security;

public sealed class ClassOwnerHandler : AuthorizationHandler<ClassOwnerRequirement>
{
    private readonly SchoolDbContext _db;

    public ClassOwnerHandler(SchoolDbContext db)
    {
        _db = db;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ClassOwnerRequirement requirement)
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
            context.Succeed(requirement);
    }
}