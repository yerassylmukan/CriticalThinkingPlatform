namespace WebApi.Auth.Entities;

public class StudentProfile
{
    public string UserId { get; set; } = default!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime? BirthDate { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
}