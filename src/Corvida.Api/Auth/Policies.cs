namespace Corvida.Api.Auth;

public static class Policies
{
    /// <summary>Any caller when the gate is open; any authenticated caller when it is closed.</summary>
    public const string User = "CorvidaUser";

    /// <summary>Requires the gate to be closed AND the caller to hold the Admin role.</summary>
    public const string Admin = "CorvidaAdmin";
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string User = "User";

    public static readonly string[] All = [Admin, User];
}
