namespace WebApi.Students.Entities;

public class ClassMember
{
    public Guid ClassId { get; set; }
    public string UserId { get; set; } = default!;
    public string RoleInClass { get; set; } = "Student";
    public DateTime JoinedUtc { get; set; } = DateTime.UtcNow;
}