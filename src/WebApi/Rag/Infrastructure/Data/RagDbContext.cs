using Microsoft.EntityFrameworkCore;
using WebApi.Rag.Entities;

namespace WebApi.Rag.Infrastructure.Data;

public class RagDbContext : DbContext
{
    public RagDbContext(DbContextOptions<RagDbContext> options) : base(options)
    {
    }

    public DbSet<Topic> Topics => Set<Topic>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<GeneratedAnswer> GeneratedAnswers => Set<GeneratedAnswer>();
    public DbSet<RagDocument> RagDocuments => Set<RagDocument>();
    public DbSet<StudentSession> StudentSessions => Set<StudentSession>();
    public DbSet<StudentResponse> StudentResponses => Set<StudentResponse>();
    public DbSet<Evaluation> Evaluations => Set<Evaluation>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasPostgresExtension("vector");

        b.Entity<Topic>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.CreatedUtc).IsRequired();
            e.Property(x => x.Conspect).HasColumnType("text");
            e.Property(x => x.TeacherId).HasMaxLength(128);
            e.HasMany(x => x.Questions)
                .WithOne(x => x.Topic)
                .HasForeignKey(x => x.TopicId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Question>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasMaxLength(4000).IsRequired();
            e.HasMany(x => x.Generated)
                .WithOne(x => x.Question)
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<GeneratedAnswer>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Text).HasMaxLength(8000).IsRequired();
            e.Property(x => x.Level).IsRequired();
        });

        b.Entity<RagDocument>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Content).HasMaxLength(10000).IsRequired();
            e.Property(x => x.Source).HasMaxLength(512);
            e.Property(x => x.Embedding).HasColumnType("vector(768)");
        });

        b.Entity<StudentSession>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.StudentId).HasMaxLength(128).IsRequired();
            e.Property(x => x.StartedUtc).IsRequired();

            e.HasOne(x => x.Topic)
                .WithMany()
                .HasForeignKey(x => x.TopicId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Evaluation)
                .WithOne(x => x.Session)
                .HasForeignKey<Evaluation>(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<StudentResponse>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Answer).HasMaxLength(8000).IsRequired();

            e.HasOne(x => x.Session)
                .WithMany(s => s.Responses)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => new { x.SessionId, x.QuestionId }).IsUnique();
        });

        b.Entity<Evaluation>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.TotalScore).HasColumnType("numeric(5,2)").IsRequired();
            e.Property(x => x.ReportJson).HasColumnType("jsonb").IsRequired();
        });
    }
}