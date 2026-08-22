using System;

namespace Corvida.Exceptions;

/// <summary>
/// The Corvida server requires authentication and the current request had no valid credentials
/// (HTTP 401). Derives from <see cref="InvalidOperationException"/> so callers that already
/// handle service errors generically keep working.
/// </summary>
public class CorvidaAuthenticationException(string message, Exception? inner = null)
    : InvalidOperationException(message, inner);
