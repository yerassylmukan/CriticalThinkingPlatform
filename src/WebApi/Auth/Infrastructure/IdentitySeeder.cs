using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApi.Auth.Entities;

namespace WebApi.Auth.Infrastructure;

public static class IdentitySeeder
{
    public static async Task SeedAsync(RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager)
    {
        var roles = new[] { "Teacher", "Student" };
        foreach (var r in roles)
            if (!await roleManager.RoleExistsAsync(r))
                await roleManager.CreateAsync(new ApplicationRole(r));

        const string email = "teacher@test.local";
        var user = await userManager.Users.SingleOrDefaultAsync(u => u.Email == email);
        if (user is null)
        {
            user = new ApplicationUser { UserName = email, Email = email, EmailConfirmed = true };
            await userManager.CreateAsync(user, "Password!123");
            await userManager.AddToRoleAsync(user, "Teacher");
        }
    }
}