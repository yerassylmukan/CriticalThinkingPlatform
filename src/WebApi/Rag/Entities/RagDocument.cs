using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using VectorEmbedding = Pgvector.Vector;

namespace WebApi.Rag.Entities;

public class RagDocument
{
    [Key] public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(512)] public string? Source { get; set; }
    [MaxLength(10000)] public string Content { get; set; } = default!;
    [Column(TypeName = "vector(768)")] public VectorEmbedding? Embedding { get; set; }
}