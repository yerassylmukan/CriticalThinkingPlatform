using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace WebApi.Students.Application;

public class InviteOptions
{
    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public string Hs256Secret { get; set; } = default!;
    public TimeSpan Ttl { get; set; } = TimeSpan.FromDays(7);
}

public sealed class InviteService
{
    private readonly InviteOptions _opts;
    private readonly ILogger<InviteService> _log;
    
    public InviteService(IOptions<InviteOptions> opts, ILogger<InviteService> log)
    {
        _opts = opts.Value;
        _log = log;
    }
    
    public (string token, DateTime expiresUtc) CreateInvite(Guid classId, string ownerTeacherId, string? emailHint)
    {
        var now = DateTime.UtcNow;
        var expires = now.Add(_opts.Ttl);
        
        var claims = new List<Claim>
        {
            new("typ", "class-invite"),
            new("classId", classId.ToString()),
            new("owner", ownerTeacherId),
            new(JwtRegisteredClaimNames.Iat, new DateTimeOffset(now).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };
        if (!string.IsNullOrWhiteSpace(emailHint))
            claims.Add(new Claim("email", emailHint));
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Hs256Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var jwt = new JwtSecurityToken(
            issuer: _opts.Issuer,
            audience: _opts.Audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds);
        
        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        _log.LogInformation("Invite created for class {ClassId} by {Owner}", classId, ownerTeacherId);
        return (token, expires);
    }

    public (Guid classId, string? ownerTeacherId, string? emailHint)? ValidateInvite(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Hs256Secret));
        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidIssuer = _opts.Issuer,
                ValidAudience = _opts.Audience,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            }, out var validated);
            
            if (principal.FindFirst("typ")?.Value != "class-invite")
                return null;
            
            var cid = principal.FindFirst("classId")?.Value;
            var owner = principal.FindFirst("owner")?.Value;
            var email = principal.FindFirst("email")?.Value;
            
            return Guid.TryParse(cid, out var classId)
                ? (classId, owner, email)
                : null;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Invite validation failed");
            return null;
        }
    }
}
