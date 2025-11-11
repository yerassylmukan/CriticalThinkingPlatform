using Microsoft.AspNetCore.Identity;

namespace WebApi.Auth.Entities;

public class ApplicationUser : IdentityUser
{
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public StudentProfile? StudentProfile { get; set; }
    public TeacherProfile? TeacherProfile { get; set; }
}