using System.ComponentModel.DataAnnotations;

namespace WebApi.Rag.Entities;

public class StudentSession
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(128)] public string StudentId { get; set; } = default!;
    public Guid TopicId { get; set; }
    public Topic Topic { get; set; } = default!;
    public DateTime StartedUtc { get; set; } = DateTime.UtcNow;
    public List<StudentResponse> Responses { get; set; } = new();
    public Evaluation? Evaluation { get; set; }
}