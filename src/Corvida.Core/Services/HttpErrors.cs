using System;
using System.Net;
using System.Net.Http;
using Corvida.Exceptions;

namespace Corvida.Services;

/// <summary>
/// Turns transport-level failures from the Corvida API into exceptions carrying a message the UI
/// can show verbatim.
/// </summary>
/// <remarks>
/// A 401/403 surfaces as an <see cref="HttpRequestException"/> just like a connection failure does,
/// so without this the HTTP services would report every auth failure as "Check your Server URL
/// setting" — pointing the user at the one thing that is not wrong.
/// </remarks>
internal static class HttpErrors
{
    public static Exception Translate(Exception ex, string serverBaseUrl) => ex switch
    {
        HttpRequestException { StatusCode: HttpStatusCode.Unauthorized } e
            => new CorvidaAuthenticationException("Your session has expired. Please sign in again.", e),
        HttpRequestException { StatusCode: HttpStatusCode.Forbidden } e
            => new CorvidaAuthorizationException("You don't have permission to do this.", e),
        _ => new InvalidOperationException(
            $"Cannot reach Corvida server at {serverBaseUrl}. Check your Server URL setting.", ex),
    };
}
