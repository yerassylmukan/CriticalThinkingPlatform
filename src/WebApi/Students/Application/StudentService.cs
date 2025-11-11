using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebApi.Auth.Entities;
using WebApi.Auth.Infrastructure.Data;
using WebApi.Students.Entities;

namespace WebApi.Students.Application;

public sealed class StudentService
{
    private readonly SchoolDbContext _db;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;

    public StudentService(
        SchoolDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        _db = db;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<(IReadOnlyList<StudentListItem> items, int total)> SearchAsync(
        string? teacherId, string? q, Guid? classId, int page, int pageSize, CancellationToken ct = default)
    {
        var query = from u in _db.Users
            join sp in _db.StudentProfiles on u.Id equals sp.UserId into spg
            from sp in spg.DefaultIfEmpty()
            select new { u, sp };

        var studentRole = await _db.Roles.Where(r => r.Name == "Student").Select(r => r.Id).SingleOrDefaultAsync(ct);
        if (studentRole is null)
            return (Array.Empty<StudentListItem>(), 0);

        query = from x in query
            join ur in _db.UserRoles on x.u.Id equals ur.UserId
            where ur.RoleId == studentRole
            select x;

        if (classId.HasValue)
        {
            var classMembers = _db.ClassMembers.Where(m => m.ClassId == classId.Value);
            query = from x in query
                join m in classMembers on x.u.Id equals m.UserId
                select x;
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = q.Trim().ToLower();
            query = query.Where(x =>
                (x.u.Email != null && x.u.Email.ToLower().Contains(like)) ||
                (x.sp!.FirstName != null && x.sp.FirstName.ToLower().Contains(like)) ||
                (x.sp!.LastName != null && x.sp.LastName.ToLower().Contains(like)));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(x => x.sp!.LastName).ThenBy(x => x.sp!.FirstName).ThenBy(x => x.u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new StudentListItem(
                x.u.Id,
                x.u.Email ?? "",
                x.sp!.FirstName,
                x.sp!.LastName,
                x.u.LockoutEnd.HasValue && x.u.LockoutEnd > DateTimeOffset.UtcNow
            ))
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<(bool ok, string? error, string? userId, string? tempPassword)> CreateStudentAsync(
        string teacherId,
        string email,
        string? password,
        string? firstName,
        string? lastName,
        IEnumerable<Guid>? classIds,
        CancellationToken ct = default)
    {
        var existing = await _userManager.FindByEmailAsync(email);
        if (existing != null)
            return (false, "email_already_exists", null, null);

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };
        var tempPass = password ?? GenerateTempPassword();
        var create = await _userManager.CreateAsync(user, tempPass);
        if (!create.Succeeded)
            return (false, string.Join("; ", create.Errors.Select(e => e.Code)), null, null);

        if (!await _roleManager.RoleExistsAsync("Student"))
            await _roleManager.CreateAsync(new ApplicationRole("Student"));
        await _userManager.AddToRoleAsync(user, "Student");

        _db.StudentProfiles.Add(new StudentProfile
        {
            UserId = user.Id,
            FirstName = firstName,
            LastName = lastName
        });

        if (classIds != null)
        {
            var allowed = await _db.Classes
                .Where(c => classIds.Contains(c.Id) && c.OwnerTeacherId == teacherId)
                .Select(c => c.Id)
                .ToListAsync(ct);

            foreach (var cid in allowed)
                _db.ClassMembers.Add(new ClassMember
                {
                    ClassId = cid,
                    UserId = user.Id,
                    RoleInClass = "Student",
                    JoinedUtc = DateTime.UtcNow
                });
        }

        await _db.SaveChangesAsync(ct);
        return (true, null, user.Id, password == null ? tempPass : null);
    }

    public async Task<StudentDetails?> GetAsync(string userId, CancellationToken ct = default)
    {
        var u = await _db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (u is null) return null;

        var sp = await _db.StudentProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId, ct);

        var cls = await (from m in _db.ClassMembers
                join c in _db.Classes on m.ClassId equals c.Id
                where m.UserId == userId
                orderby c.Name
                select new StudentClassItem(c.Id, c.Name, c.Grade, c.Year))
            .ToListAsync(ct);

        var locked = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow;

        return new StudentDetails(
            u.Id, u.Email ?? "",
            sp?.FirstName, sp?.LastName,
            locked, cls);
    }

    public async Task<(bool ok, string? error)> UpdateAsync(
        string teacherId,
        string userId,
        string? firstName,
        string? lastName,
        IEnumerable<Guid>? classesAdd,
        IEnumerable<Guid>? classesRemove,
        CancellationToken ct = default)
    {
        var u = await _db.Users.SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (u is null) return (false, "not_found");

        var sp = await _db.StudentProfiles.SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (sp is null)
        {
            sp = new StudentProfile { UserId = userId };
            _db.StudentProfiles.Add(sp);
        }

        if (firstName != null) sp.FirstName = firstName;
        if (lastName != null) sp.LastName = lastName;

        if (classesRemove != null)
        {
            var ownedToRemove = await _db.Classes
                .Where(c => classesRemove.Contains(c.Id) && c.OwnerTeacherId == teacherId)
                .Select(c => c.Id)
                .ToListAsync(ct);

            var rm = await _db.ClassMembers
                .Where(m => m.UserId == userId && ownedToRemove.Contains(m.ClassId))
                .ToListAsync(ct);

            _db.ClassMembers.RemoveRange(rm);
        }

        if (classesAdd != null)
        {
            var ownedToAdd = await _db.Classes
                .Where(c => classesAdd.Contains(c.Id) && c.OwnerTeacherId == teacherId)
                .Select(c => c.Id)
                .ToListAsync(ct);

            var existing = await _db.ClassMembers
                .Where(m => m.UserId == userId && ownedToAdd.Contains(m.ClassId))
                .Select(m => m.ClassId)
                .ToListAsync(ct);

            var newOnes = ownedToAdd.Except(existing);
            foreach (var cid in newOnes)
                _db.ClassMembers.Add(new ClassMember
                {
                    ClassId = cid,
                    UserId = userId,
                    RoleInClass = "Student",
                    JoinedUtc = DateTime.UtcNow
                });
        }

        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task<(bool ok, string? error)> DeactivateAsync(string userId, CancellationToken ct = default)
    {
        var u = await _userManager.FindByIdAsync(userId);
        if (u is null) return (false, "not_found");

        u.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
        var res = await _userManager.UpdateAsync(u);
        return res.Succeeded ? (true, null) : (false, string.Join("; ", res.Errors.Select(e => e.Code)));
    }

    public async Task<(bool ok, string? error, string? tempPassword)> ResetPasswordAsync(string userId,
        CancellationToken ct = default)
    {
        var u = await _userManager.FindByIdAsync(userId);
        if (u is null) return (false, "not_found", null);

        var token = await _userManager.GeneratePasswordResetTokenAsync(u);
        var temp = GenerateTempPassword();
        var res = await _userManager.ResetPasswordAsync(u, token, temp);
        return res.Succeeded
            ? (true, null, temp)
            : (false, string.Join("; ", res.Errors.Select(e => e.Code)), null);
    }

    private static string GenerateTempPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        var rng = Random.Shared;
        var middle = new string(Enumerable.Range(0, 6).Select(_ => chars[rng.Next(chars.Length)]).ToArray());
        return $"Abc-{middle}-9!";
    }
}

public record StudentListItem(string UserId, string Email, string? FirstName, string? LastName, bool IsLocked);

public record StudentClassItem(Guid ClassId, string Name, int? Grade, int? Year);

public record StudentDetails(
    string UserId,
    string Email,
    string? FirstName,
    string? LastName,
    bool IsLocked,
    IReadOnlyList<StudentClassItem> Classes);