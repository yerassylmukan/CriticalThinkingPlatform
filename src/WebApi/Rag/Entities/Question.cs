using System.ComponentModel.DataAnnotations;

namespace WebApi.Rag.Entities;

public class Question
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(4000)] public string Text { get; set; } = default!;
    public Guid TopicId { get; set; }
    public Topic Topic { get; set; } = default!;
    public List<GeneratedAnswer> Generated { get; set; } = new();
}