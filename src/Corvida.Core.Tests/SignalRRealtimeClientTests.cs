using System.Net;
using System.Net.Sockets;
using Corvida.Models;
using Corvida.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Corvida.Core.Tests;

public class SignalRRealtimeClientTests
{
    // Port 1 is reserved and never listening, so negotiate fails immediately.
    private const string UnreachableServer = "http://127.0.0.1:1";

    private static SignalRRealtimeClient Create(StorageMode mode, string? serverUrl = UnreachableServer)
    {
        var settings = new FakeSettingsService();
        settings.Settings.StorageMode = mode;
        settings.Settings.ServerUrl = serverUrl;
        return new SignalRRealtimeClient(
            settings, new SignedOutTokenProvider(), NullLogger<SignalRRealtimeClient>.Instance);
    }

    [Fact]
    public async Task StartAsync_InLocalFolderMode_DoesNothing()
    {
        var sut = Create(StorageMode.LocalFolder);

        await sut.StartAsync();     // must not throw and must not attempt a connection
    }

    [Fact]
    public async Task StartAsync_WhenServerUnreachable_DoesNotThrow()
    {
        var sut = Create(StorageMode.ServerHosted);

        // Hosts call this fire-and-forget (`_ = realtime.StartAsync()`), so throwing here would
        // surface as an unobserved task exception rather than a handled failure.
        await sut.StartAsync();
    }

    [Fact]
    public async Task StartAsync_AfterFailedAttempt_RetriesInsteadOfGivingUp()
    {
        // The old implementation assigned _connection *before* StartAsync and only cleared it in
        // StopAsync, so a server that was down at launch was never retried for the process
        // lifetime. Counting negotiate hits is the only way to see the difference from outside.
        using var server = new CountingNegotiateServer();
        var sut = Create(StorageMode.ServerHosted, server.BaseUrl);

        await sut.StartAsync();
        Assert.Equal(1, server.NegotiateCount);

        await sut.StartAsync();
        Assert.Equal(2, server.NegotiateCount);
    }

    [Fact]
    public async Task StopAsync_WithoutStart_DoesNotThrow()
    {
        var sut = Create(StorageMode.ServerHosted);

        await sut.StopAsync();
    }

    [Fact]
    public async Task RestartAsync_DoesNotThrow()
    {
        var sut = Create(StorageMode.ServerHosted);

        await sut.StartAsync();
        await sut.RestartAsync();
    }

    [Fact]
    public async Task ConcurrentStartAndStop_DoNotThrow()
    {
        var sut = Create(StorageMode.ServerHosted);

        await Task.WhenAll(
            sut.StartAsync(), sut.StartAsync(), sut.StopAsync(), sut.RestartAsync());
    }

    [Fact]
    public void IsResolvableFromTheHostServiceCollection()
    {
        // Mirrors how Corvida (desktop) and Corvida.Web register it. The client now takes an
        // ILogger<T>, which is only available because AddHttpClient pulls in AddLogging — this
        // guards against that dependency quietly disappearing.
        var services = new ServiceCollection();
        services.AddSingleton<ISettingsService, FakeSettingsService>();
        services.AddSingleton<ITokenStore, InMemoryTokenStore>();
        services.AddSingleton<IAuthClient, AuthClient>();
        services.AddSingleton<ITokenProvider, TokenProvider>();
        services.AddSingleton<IRealtimeClient, SignalRRealtimeClient>();
        services.AddHttpClient("CorvidaApi");

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IRealtimeClient>());
    }
}

/// <summary>
/// Answers SignalR's negotiate request with 404 so the connection attempt fails fast, while
/// counting how many attempts were actually made.
/// </summary>
internal sealed class CountingNegotiateServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private int _negotiateCount;

    public string BaseUrl { get; }

    public int NegotiateCount => Volatile.Read(ref _negotiateCount);

    public CountingNegotiateServer()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        BaseUrl = $"http://127.0.0.1:{port}";
        _listener.Prefixes.Add($"{BaseUrl}/");
        _listener.Start();
        _ = Task.Run(AcceptLoopAsync);
    }

    private async Task AcceptLoopAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { return; }   // listener stopped

            if (ctx.Request.Url?.AbsolutePath.EndsWith("/negotiate", StringComparison.Ordinal) == true)
                Interlocked.Increment(ref _negotiateCount);

            ctx.Response.StatusCode = 404;
            ctx.Response.Close();
        }
    }

    public void Dispose()
    {
        _listener.Stop();
        ((IDisposable)_listener).Dispose();
    }
}
