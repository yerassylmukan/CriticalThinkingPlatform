using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using WebApi.Rag.Infrastructure.Data;

namespace WebApi.Rag.Security;

public sealed class SessionOwnerHandler : AuthorizationHandler<SessionOwnerRequirement>
{
    private readonly RagDbContext _rag;
    public SessionOwnerHandler(RagDbContext rag) => _rag = rag;

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, SessionOwnerRequirement requirement)
    {
        if (context.Resource is not HttpContext http) return;

        var uid = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(uid)) return;

        var idStr = http.Request.RouteValues.TryGetValue("id", out var val) ? val?.ToString() : null;
        if (!Guid.TryParse(idStr, out var sessionId)) return;

        var isOwner = await _rag.StudentSessions.AsNoTracking()
            .AnyAsync(s => s.Id == sessionId && s.StudentId == uid);

        if (isOwner) context.Succeed(requirement);
    }
}