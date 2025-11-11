namespace WebApi.Students.Entities;

public class Class
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = default!;
    public int? Grade { get; set; }
    public int? Year { get; set; }

    public string OwnerTeacherId { get; set; } = default!;
}