using System.ComponentModel.DataAnnotations;
using WebApi.Rag.Enums;

namespace WebApi.Rag.Entities;

public class GeneratedAnswer
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    public Guid QuestionId { get; set; }
    public Question Question { get; set; } = default!;
    public AnswerLevel Level { get; set; }
    [MaxLength(8000)] public string Text { get; set; } = default!;
}