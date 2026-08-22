using Microsoft.AspNetCore.Identity;

namespace Corvida.Api.Auth;

/// <summary>
/// A login account. Deliberately unrelated to <see cref="Corvida.Models.Agent"/>: an Agent is a
/// board member (human or AI), a CorvidaUser is only ever an access-control subject.
/// </summary>
public class CorvidaUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}
