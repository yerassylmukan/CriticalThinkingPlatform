using Microsoft.AspNetCore.Authorization;

namespace WebApi.Students.Security;

public sealed class ClassMemberOrOwnerRequirement : IAuthorizationRequirement
{
}