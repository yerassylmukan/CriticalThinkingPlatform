using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WebApi.Auth.Entities;
using WebApi.Auth.Infrastructure.Data;

namespace WebApi.Auth.Application;

public class JwtTokenService(
    IOptions<JwtOptions> jwtOptions,
    UserManager<ApplicationUser> userManager,
    SchoolDbContext db,
    ILogger<JwtTokenService> logger)
{
    private readonly JwtOptions _opts = jwtOptions.Value;

    public async Task<(string accessToken, string refreshToken)> IssueAsync(ApplicationUser user, string? ip,
        string? device, CancellationToken ct = default)
    {
        var roles = await userManager.GetRolesAsync(user);
        var access = GenerateAccessToken(user, roles);
        var refresh = await GenerateAndStoreRefreshTokenAsync(user, ip, device, ct);
        return (access, refresh);
    }

    public string GenerateAccessToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var now = DateTime.UtcNow;
        var jti = Guid.NewGuid().ToString("N");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, jti),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName ?? user.Email ?? user.Id),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Hs256Secret!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            _opts.Issuer,
            _opts.Audience,
            claims,
            now,
            now.Add(_opts.AccessTtl),
            creds);

        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }

    public async Task<string> GenerateAndStoreRefreshTokenAsync(ApplicationUser user, string? ip, string? device,
        CancellationToken ct)
    {
        var raw = GenerateSecureRandomToken();
        var hash = Hash(raw);

        var entity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = hash,
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.Add(_opts.RefreshTtl),
            DeviceInfo = device,
            Ip = ip
        };
        db.RefreshTokens.Add(entity);
        await db.SaveChangesAsync(ct);
        return raw;
    }

    public async Task<(string accessToken, string refreshToken)?> RotateAsync(string refreshTokenRaw, string? ip,
        string? device, CancellationToken ct = default)
    {
        var hash = Hash(refreshTokenRaw);
        var rt = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (rt is null || !rt.IsActive)
            return null;

        var user = await userManager.Users.SingleOrDefaultAsync(u => u.Id == rt.UserId, ct);
        if (user is null)
            return null;

        rt.RevokedUtc = DateTime.UtcNow;

        var newRaw = GenerateSecureRandomToken();
        var newHash = Hash(newRaw);
        var newRt = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newHash,
            CreatedUtc = DateTime.UtcNow,
            ExpiresUtc = DateTime.UtcNow.Add(_opts.RefreshTtl),
            Ip = ip,
            DeviceInfo = device
        };
        db.RefreshTokens.Add(newRt);
        rt.ReplacedByTokenId = newRt.Id;

        await db.SaveChangesAsync(ct);

        var roles = await userManager.GetRolesAsync(user);
        var access = GenerateAccessToken(user, roles);
        return (access, newRaw);
    }

    public async Task<bool> RevokeAsync(string refreshTokenRaw, CancellationToken ct = default)
    {
        var hash = Hash(refreshTokenRaw);
        var rt = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash, ct);
        if (rt is null || !rt.IsActive)
            return false;

        rt.RevokedUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static string GenerateSecureRandomToken()
    {
        var bytes = new byte[64];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    public static string Hash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes);
    }
}