using System.ComponentModel.DataAnnotations;

namespace WebApi.Rag.Entities;

public class Topic
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(200)] public string Title { get; set; } = default!;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public string? Conspect { get; set; }
    [MaxLength(128)] public string? TeacherId { get; set; }
    public List<Question> Questions { get; set; } = new();
}