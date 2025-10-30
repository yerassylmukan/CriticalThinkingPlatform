using System.ComponentModel.DataAnnotations;

namespace WebApi.Rag.Entities;

public class StudentResponse
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public StudentSession Session { get; set; } = default!;
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = default!;
    [MaxLength(8000)] public string Answer { get; set; } = default!;
}