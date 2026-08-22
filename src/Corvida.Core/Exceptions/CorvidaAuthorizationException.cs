using System;

namespace Corvida.Exceptions;

/// <summary>
/// The caller is authenticated but lacks the role required for the operation (HTTP 403).
/// Derives from <see cref="InvalidOperationException"/> so callers that already handle service
/// errors generically keep working.
/// </summary>
public class CorvidaAuthorizationException(string message, Exception? inner = null)
    : InvalidOperationException(message, inner);
