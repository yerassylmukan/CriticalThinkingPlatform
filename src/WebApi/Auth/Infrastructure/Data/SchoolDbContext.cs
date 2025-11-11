using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WebApi.Auth.Entities;
using WebApi.Students.Entities;

namespace WebApi.Auth.Infrastructure.Data;

public class SchoolDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
{
    public SchoolDbContext(DbContextOptions<SchoolDbContext> options) : base(options)
    {
    }

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<TeacherProfile> TeacherProfiles => Set<TeacherProfile>();
    public DbSet<Class> Classes => Set<Class>();
    public DbSet<ClassMember> ClassMembers => Set<ClassMember>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<ApplicationUser>(e => { e.Property(x => x.CreatedUtc).IsRequired(); });

        b.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.UserId).IsRequired();
            e.Property(x => x.TokenHash).HasMaxLength(256).IsRequired();
            e.Property(x => x.CreatedUtc).IsRequired();
            e.Property(x => x.ExpiresUtc).IsRequired();

            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasIndex(x => new { x.UserId, x.ExpiresUtc });
        });

        b.Entity<StudentProfile>(e =>
        {
            e.HasKey(x => x.UserId);
            e.Property(x => x.FirstName).HasMaxLength(100);
            e.Property(x => x.LastName).HasMaxLength(100);
            e.Property(x => x.AvatarUrl).HasMaxLength(512);
        });

        b.Entity<TeacherProfile>(e =>
        {
            e.HasKey(x => x.UserId);
            e.Property(x => x.FirstName).HasMaxLength(100);
            e.Property(x => x.LastName).HasMaxLength(100);
            e.Property(x => x.Department).HasMaxLength(200);
        });

        b.Entity<Class>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.OwnerTeacherId).IsRequired();
            e.HasIndex(x => new { x.OwnerTeacherId, x.Name }).IsUnique(false);
        });

        b.Entity<ClassMember>(e =>
        {
            e.HasKey(x => new { x.ClassId, x.UserId });
            e.Property(x => x.RoleInClass).HasMaxLength(32).IsRequired();
            e.Property(x => x.JoinedUtc).IsRequired();

            e.HasIndex(x => x.ClassId);
            e.HasIndex(x => x.UserId);
        });
    }
}