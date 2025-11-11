namespace WebApi.Auth.Entities;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = default!;
    public string TokenHash { get; set; } = default!;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
    public Guid? ReplacedByTokenId { get; set; }

    public string? DeviceInfo { get; set; }
    public string? Ip { get; set; }

    public bool IsActive => RevokedUtc == null && DateTime.UtcNow < ExpiresUtc;
}