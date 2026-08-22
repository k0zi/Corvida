namespace Corvida.Api.Auth;

/// <summary>
/// Single-row table (Id is pinned to 1 by a check constraint) holding the one piece of state that
/// decides whether the whole API requires authentication.
/// </summary>
public class SystemSettingsEntity
{
    public int Id { get; set; } = 1;

    public bool AuthRequired { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UnixEpoch;
}
