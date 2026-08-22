using Microsoft.AspNetCore.Authorization;

namespace Corvida.Api.Auth;

/// <param name="Role">Required role, or <c>null</c> to accept any authenticated principal.</param>
public sealed record CorvidaAccessRequirement(string? Role = null) : IAuthorizationRequirement;
