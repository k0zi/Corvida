using System.Net;
using Corvida.Models;
using Corvida.Services;

namespace Corvida.Core.Tests;

internal sealed class FakeSettingsService : ISettingsService
{
    public AppSettings Settings { get; set; } = new AppSettings();
    public Task LoadAsync() => Task.CompletedTask;
    public Task SaveAsync() => Task.CompletedTask;
}

internal sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), System.IO.Path.GetRandomFileName());

    public TempDir() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        if (Directory.Exists(Path))
            Directory.Delete(Path, recursive: true);
    }
}

internal sealed class TestHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();
    public List<HttpRequestMessage> Requests { get; } = [];

    public void Enqueue(HttpResponseMessage response) => _responses.Enqueue(response);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (_responses.Count == 0)
            throw new InvalidOperationException($"No response queued for {request.Method} {request.RequestUri}");
        return Task.FromResult(_responses.Dequeue());
    }
}

internal sealed class ThrowingHttpMessageHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromException<HttpResponseMessage>(new HttpRequestException("simulated connection failure"));
}

internal sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public FakeHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

    public HttpClient CreateClient(string name) =>
        new HttpClient(_handler, disposeHandler: false);
}
