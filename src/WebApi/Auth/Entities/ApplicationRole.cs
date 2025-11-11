using Microsoft.AspNetCore.Identity;

namespace WebApi.Auth.Entities;

public class ApplicationRole : IdentityRole
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string name) : base(name)
    {
    }
}