namespace WebApi.Auth.Entities;

public class TeacherProfile
{
    public string UserId { get; set; } = default!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Department { get; set; }
}