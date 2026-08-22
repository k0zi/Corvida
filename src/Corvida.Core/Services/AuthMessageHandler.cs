using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Corvida.Services;

/// <summary>
/// Attaches the bearer token to every Corvida API request, and retries once after refreshing when
/// the server reports the token is no longer good.
/// </summary>
/// <remarks>
/// Registered on the "CorvidaApi" named client, so the Http*Service classes need no changes — each
/// resolves its HttpClient from the factory per call and picks this up automatically.
/// </remarks>
public sealed class AuthMessageHandler(ITokenProvider tokens) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Never overwrite an explicitly supplied header (the token endpoint sets none, but a
        // caller may be doing something deliberate).
        if (request.Headers.Authorization is not null)
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        var token = await tokens.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Unauthorized || token is null)
            return response;

        // One retry only. If the refreshed token is also rejected the session is genuinely over,
        // and retrying again would loop.
        if (!await tokens.TryRefreshAsync(cancellationToken).ConfigureAwait(false))
            return response;

        var refreshed = await tokens.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        if (refreshed is null) return response;

        response.Dispose();

        var retry = await CloneAsync(request).ConfigureAwait(false);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);
        return await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// A sent HttpRequestMessage cannot be reused, so the retry needs a fresh copy.
    /// </summary>
    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        foreach (var option in request.Options)
            clone.Options.TryAdd(option.Key, option.Value);

        if (request.Content is not null)
        {
            var buffered = await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            var content = new ByteArrayContent(buffered);

            foreach (var header in request.Content.Headers)
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);

            clone.Content = content;
        }

        return clone;
    }
}
