namespace Corvida.Api.Auth;

public static class CorvidaSchemes
{
    /// <summary>
    /// The API's default authenticate/challenge scheme. A policy scheme that forwards to OpenIddict's
    /// token validation, or — once personal access tokens exist — to the PAT handler.
    /// </summary>
    public const string Dispatch = "Corvida";

    /// <summary>Long-lived tokens for non-interactive clients (the MCP server).</summary>
    public const string PersonalAccessToken = "CorvidaPat";

    /// <summary>Marks a personal access token, so the dispatch scheme can route on it.</summary>
    public const string PatPrefix = "corv_pat_";
}
