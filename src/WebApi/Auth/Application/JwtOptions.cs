namespace WebApi.Auth.Application;

public class JwtOptions
{
    public string Issuer { get; set; } = default!;
    public string Audience { get; set; } = default!;
    public string? Hs256Secret { get; set; }
    public TimeSpan AccessTtl { get; set; } = TimeSpan.FromMinutes(20);
    public TimeSpan RefreshTtl { get; set; } = TimeSpan.FromDays(14);
}