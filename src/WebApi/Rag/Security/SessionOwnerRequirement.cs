using Microsoft.AspNetCore.Authorization;

namespace WebApi.Rag.Security;

public sealed class SessionOwnerRequirement : IAuthorizationRequirement { }