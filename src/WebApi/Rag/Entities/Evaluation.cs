namespace WebApi.Rag.Entities;

public class Evaluation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public StudentSession Session { get; set; } = default!;
    public decimal TotalScore { get; set; }
    public string ReportJson { get; set; } = default!;
}