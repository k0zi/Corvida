namespace Corvida.Api.Auth;

public static class CorvidaClaims
{
    public const string DisplayName = "display_name";

    /// <summary>ASP.NET Identity's security stamp — must never reach an issued token.</summary>
    public const string SecurityStamp = "AspNet.Identity.SecurityStamp";
}
