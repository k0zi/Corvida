namespace Corvida.Models;

/// <summary>
/// A login account, as seen by the admin UI. Distinct from <see cref="Agent"/>: an Agent is a board
/// member (human or AI), a UserAccount is only ever an access-control subject.
/// </summary>
public class UserAccount
{
    public string Id { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = UserRoles.User;
    public bool IsLockedOut { get; set; }
}

public static class UserRoles
{
    public const string Admin = "Admin";
    public const string User = "User";

    public static readonly string[] All = [Admin, User];
}
