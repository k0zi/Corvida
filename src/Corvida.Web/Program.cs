using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;

namespace Corvida.Web;

internal sealed partial class Program
{
    /// <summary>Fetched from the site root before the UI starts; absent is fine.</summary>
    private const string ConfigFile = "config.json";

    private static async Task Main(string[] args)
    {
        App.ApiBaseUrl = await ResolveApiBaseUrlAsync(args);

        await BuildAvaloniaApp()
            .WithInterFont()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>();

    /// <summary>
    /// Works out which Corvida API to talk to.
    /// </summary>
    /// <remarks>
    /// Resolved at runtime rather than baked in at build time, so one published bundle can be
    /// served from anywhere. Falling back to the page's own origin is what lets a reverse proxy
    /// serve the app and the API together — which in turn avoids CORS entirely and, over HTTPS,
    /// avoids the browser blocking calls to a plain-HTTP API as mixed content.
    /// </remarks>
    private static async Task<string> ResolveApiBaseUrlAsync(string[] args)
    {
        // main.js passes globalThis.location.href as the first argument.
        var origin = args.Length > 0 && Uri.TryCreate(args[0], UriKind.Absolute, out var href)
            ? href.GetLeftPart(UriPartial.Authority)
            : string.Empty;

        try
        {
            using var http = new HttpClient();
            var configUri = string.IsNullOrEmpty(origin) ? ConfigFile : $"{origin}/{ConfigFile}";

            var config = await http.GetFromJsonAsync<BrowserConfig>(configUri);
            if (!string.IsNullOrWhiteSpace(config?.ApiBaseUrl))
                return config.ApiBaseUrl.TrimEnd('/');
        }
        catch (Exception)
        {
            // No config.json, or it is not readable: same-origin is the sensible default.
        }

        return origin;
    }

    private sealed record BrowserConfig(
        [property: JsonPropertyName("apiBaseUrl")] string? ApiBaseUrl);
}
