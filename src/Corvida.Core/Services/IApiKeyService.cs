using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Corvida.Services;

/// <param name="Key">The raw token. Only ever populated when the key is first created.</param>
public sealed record ApiKeyInfo(
    string Id, string Name, DateTime CreatedAt, DateTime? LastUsedAt, DateTime? ExpiresAt, string? Key = null);

/// <summary>Administrator-only management of personal access tokens for non-interactive clients.</summary>
public interface IApiKeyService
{
    Task<List<ApiKeyInfo>> GetKeysAsync();

    /// <summary>Creates a key. The returned <see cref="ApiKeyInfo.Key"/> cannot be recovered later.</summary>
    Task<ApiKeyInfo> CreateKeyAsync(string name);

    Task RevokeKeyAsync(string id);
}
