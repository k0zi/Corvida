using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Corvida.Services;

/// <inheritdoc />
public class HttpApiKeyService(IHttpClientFactory factory, ISettingsService settings) : IApiKeyService
{
    private HttpClient Client => factory.CreateClient("CorvidaApi");
    private string Base => (settings.Settings.ServerUrl ?? "http://localhost:5083").TrimEnd('/');

    public async Task<List<ApiKeyInfo>> GetKeysAsync()
    {
        try
        {
            return await Client.GetFromJsonAsync<List<ApiKeyInfo>>($"{Base}/api/apikeys") ?? [];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw HttpErrors.Translate(ex, Base);
        }
    }

    public async Task<ApiKeyInfo> CreateKeyAsync(string name)
    {
        try
        {
            var response = await Client.PostAsJsonAsync($"{Base}/api/apikeys", new { Name = name });
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<ApiKeyInfo>())!;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw HttpErrors.Translate(ex, Base);
        }
    }

    public async Task RevokeKeyAsync(string id)
    {
        try
        {
            var response = await Client.DeleteAsync($"{Base}/api/apikeys/{id}");
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw HttpErrors.Translate(ex, Base);
        }
    }
}
