namespace Corvida.Api.Auth;

/// <summary>
/// A long-lived token for non-interactive clients (the MCP server). Only the SHA-256 hash is
/// stored; the raw value is shown to the admin once, at creation.
/// </summary>
public class PersonalAccessTokenEntity
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string OwnerUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
