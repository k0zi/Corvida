using System.Net;
using System.Net.Http.Headers;
using Corvida.Services;

namespace Corvida.Core.Tests;

/// <summary>
/// The handler is exercised directly rather than through Http*Service, because
/// <see cref="FakeHttpClientFactory"/> returns a bare HttpClient and bypasses the DelegatingHandler
/// pipeline entirely — those tests would stay green whether or not this works.
/// </summary>
public class AuthMessageHandlerTests
{
    private static HttpRequestMessage Request() =>
        new(HttpMethod.Get, "http://test-host/api/boards");

    private static (HttpMessageInvoker Invoker, ScriptedHandler Inner) Build(ITokenProvider tokens)
    {
        var inner = new ScriptedHandler();
        var handler = new AuthMessageHandler(tokens) { InnerHandler = inner };
        return (new HttpMessageInvoker(handler), inner);
    }

    [Fact]
    public async Task AttachesTheBearerToken()
    {
        var tokens = new FakeTokenProvider("token-1");
        var (invoker, inner) = Build(tokens);
        inner.Enqueue(HttpStatusCode.OK);

        await invoker.SendAsync(Request(), CancellationToken.None);

        Assert.Equal("Bearer token-1", inner.Requests[0].Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task SendsNoHeader_WhenSignedOut()
    {
        var (invoker, inner) = Build(new SignedOutTokenProvider());
        inner.Enqueue(HttpStatusCode.OK);

        await invoker.SendAsync(Request(), CancellationToken.None);

        Assert.Null(inner.Requests[0].Headers.Authorization);
    }

    [Fact]
    public async Task DoesNotOverwriteAnExplicitHeader()
    {
        var tokens = new FakeTokenProvider("token-1");
        var (invoker, inner) = Build(tokens);
        inner.Enqueue(HttpStatusCode.OK);

        var request = Request();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "caller-supplied");
        await invoker.SendAsync(request, CancellationToken.None);

        Assert.Equal("Bearer caller-supplied", inner.Requests[0].Headers.Authorization?.ToString());
    }

    [Fact]
    public async Task RefreshesAndRetriesOnce_AfterA401()
    {
        var tokens = new FakeTokenProvider("stale") { RefreshedToken = "fresh" };
        var (invoker, inner) = Build(tokens);
        inner.Enqueue(HttpStatusCode.Unauthorized);
        inner.Enqueue(HttpStatusCode.OK);

        var response = await invoker.SendAsync(Request(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Requests.Count);
        Assert.Equal("Bearer stale", inner.Requests[0].Headers.Authorization?.ToString());
        Assert.Equal("Bearer fresh", inner.Requests[1].Headers.Authorization?.ToString());
        Assert.Equal(1, tokens.RefreshCount);
    }

    [Fact]
    public async Task GivesUpAfterASecond401_RatherThanLooping()
    {
        var tokens = new FakeTokenProvider("stale") { RefreshedToken = "also-stale" };
        var (invoker, inner) = Build(tokens);
        inner.Enqueue(HttpStatusCode.Unauthorized);
        inner.Enqueue(HttpStatusCode.Unauthorized);

        var response = await invoker.SendAsync(Request(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(2, inner.Requests.Count);
        Assert.Equal(1, tokens.RefreshCount);
    }

    [Fact]
    public async Task DoesNotRetry_WhenRefreshFails()
    {
        var tokens = new FakeTokenProvider("stale") { RefreshedToken = null };
        var (invoker, inner) = Build(tokens);
        inner.Enqueue(HttpStatusCode.Unauthorized);

        var response = await invoker.SendAsync(Request(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(inner.Requests);
    }

    [Fact]
    public async Task ReplaysTheRequestBodyOnRetry()
    {
        var tokens = new FakeTokenProvider("stale") { RefreshedToken = "fresh" };
        var (invoker, inner) = Build(tokens);
        inner.Enqueue(HttpStatusCode.Unauthorized);
        inner.Enqueue(HttpStatusCode.OK);

        var request = new HttpRequestMessage(HttpMethod.Put, "http://test-host/api/boards/b1")
        {
            Content = new StringContent("""{"Name":"Board"}""", System.Text.Encoding.UTF8, "application/json"),
        };
        await invoker.SendAsync(request, CancellationToken.None);

        // A sent HttpRequestMessage cannot be reused, so the retry must carry a fresh copy of
        // the body — otherwise the second attempt would silently send nothing.
        Assert.Equal(2, inner.Requests.Count);
        Assert.Equal("""{"Name":"Board"}""", inner.Bodies[1]);
        Assert.Equal("application/json", inner.Requests[1].Content?.Headers.ContentType?.MediaType);
    }

    private sealed class FakeTokenProvider(string? initialToken) : ITokenProvider
    {
        private string? _token = initialToken;

        public string? RefreshedToken { get; init; }
        public int RefreshCount { get; private set; }

        public event Action? SignedOut;

        public ValueTask<string?> GetAccessTokenAsync(CancellationToken ct = default) => new(_token);

        public ValueTask<bool> TryRefreshAsync(CancellationToken ct = default)
        {
            RefreshCount++;
            _token = RefreshedToken;
            if (_token is null) SignedOut?.Invoke();
            return new(_token is not null);
        }

        public ValueTask SignInAsync(StoredTokens tokens) => ValueTask.CompletedTask;
        public ValueTask SignOutAsync() { _token = null; SignedOut?.Invoke(); return ValueTask.CompletedTask; }
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<HttpStatusCode> _responses = new();

        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string?> Bodies { get; } = [];

        public void Enqueue(HttpStatusCode status) => _responses.Enqueue(status);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            Bodies.Add(request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken));

            if (_responses.Count == 0)
                throw new InvalidOperationException($"No response queued for {request.Method} {request.RequestUri}");

            return new HttpResponseMessage(_responses.Dequeue());
        }
    }
}
