using Microsoft.AspNetCore.Authorization;

namespace Corvida.Api.Auth;

/// <summary>
/// The single gate every protected endpoint goes through.
/// </summary>
/// <remarks>
/// Authorization policies are built once at startup, but the "require login" setting has to flip
/// without restarting the process — so the decision lives here, in a handler that reads the current
/// flag, rather than in the policy's requirements.
/// <para>
/// This works because <c>PolicyEvaluator</c> reports success whenever the authorization service
/// succeeds, regardless of whether authentication produced a principal. Succeeding unconditionally
/// while the gate is open therefore lets anonymous callers straight through, exactly as the API
/// behaved before authentication existed.
/// </para>
/// </remarks>
internal sealed class CorvidaAccessHandler(IAuthGateState gate)
    : AuthorizationHandler<CorvidaAccessRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, CorvidaAccessRequirement requirement)
    {
        if (!await gate.IsAuthRequiredAsync().ConfigureAwait(false))
        {
            context.Succeed(requirement);
            return;
        }

        if (context.User.Identity?.IsAuthenticated is not true)
            return;     // -> 401 Challenge

        if (requirement.Role is null || context.User.IsInRole(requirement.Role))
            context.Succeed(requirement);
        // else -> 403 Forbid
    }
}
