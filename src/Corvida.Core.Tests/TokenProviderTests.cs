using Corvida.Services;

namespace Corvida.Core.Tests;

public class TokenProviderTests
{
    private static StoredTokens Fresh(string access = "fresh", string? refresh = "refresh-1") =>
        new(access, DateTimeOffset.UtcNow.AddMinutes(30), refresh);

    private static StoredTokens NearlyExpired(string access = "stale", string? refresh = "refresh-1") =>
        new(access, DateTimeOffset.UtcNow.AddSeconds(5), refresh);

    [Fact]
    public async Task ReturnsNull_WhenSignedOut()
    {
        var sut = new TokenProvider(new FakeAuthClient(), new InMemoryTokenStore());

        Assert.Null(await sut.GetAccessTokenAsync());
    }

    [Fact]
    public async Task ReturnsTheStoredToken_WhenStillValid()
    {
        var store = new InMemoryTokenStore();
        await store.SetAsync(Fresh());
        var auth = new FakeAuthClient();
        var sut = new TokenProvider(auth, store);

        Assert.Equal("fresh", await sut.GetAccessTokenAsync());
        Assert.Equal(0, auth.RefreshCount);
    }

    [Fact]
    public async Task RefreshesPreemptively_InsideTheExpiryWindow()
    {
        var store = new InMemoryTokenStore();
        await store.SetAsync(NearlyExpired());
        var auth = new FakeAuthClient { Next = Fresh("renewed", "refresh-2") };
        var sut = new TokenProvider(auth, store);

        // Refreshing only once the token has already expired would let a request go out with a
        // token that lapses in flight.
        Assert.Equal("renewed", await sut.GetAccessTokenAsync());
        Assert.Equal(1, auth.RefreshCount);
    }

    [Fact]
    public async Task ConcurrentCallers_ShareASingleRefresh()
    {
        var store = new InMemoryTokenStore();
        await store.SetAsync(NearlyExpired());
        var auth = new FakeAuthClient { Next = Fresh("renewed", "refresh-2"), Delay = TimeSpan.FromMilliseconds(50) };
        var sut = new TokenProvider(auth, store);

        var results = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => sut.GetAccessTokenAsync().AsTask()));

        // OpenIddict rotates refresh tokens and detects reuse: eight parallel redemptions of the
        // same refresh token would revoke the whole chain and sign the user out.
        Assert.Equal(1, auth.RefreshCount);
        Assert.All(results, r => Assert.Equal("renewed", r));
    }

    [Fact]
    public async Task SignsOut_WhenTheRefreshTokenIsRejected()
    {
        var store = new InMemoryTokenStore();
        await store.SetAsync(NearlyExpired());
        var sut = new TokenProvider(new FakeAuthClient { Next = null }, store);

        var signedOut = false;
        sut.SignedOut += () => signedOut = true;

        Assert.Null(await sut.GetAccessTokenAsync());
        Assert.True(signedOut);
        Assert.Null(await store.GetAsync());
    }

    [Fact]
    public async Task SignsOut_WhenThereIsNoRefreshToken()
    {
        var store = new InMemoryTokenStore();
        await store.SetAsync(NearlyExpired(refresh: null));
        var auth = new FakeAuthClient();
        var sut = new TokenProvider(auth, store);

        Assert.Null(await sut.GetAccessTokenAsync());
        Assert.Equal(0, auth.RefreshCount);
    }

    [Fact]
    public async Task SignOutAsync_ClearsTheStoreAndRaisesTheEvent()
    {
        var store = new InMemoryTokenStore();
        await store.SetAsync(Fresh());
        var sut = new TokenProvider(new FakeAuthClient(), store);

        var signedOut = false;
        sut.SignedOut += () => signedOut = true;

        await sut.SignOutAsync();

        Assert.Null(await store.GetAsync());
        Assert.True(signedOut);
    }

    private sealed class FakeAuthClient : IAuthClient
    {
        private int _refreshCount;

        public StoredTokens? Next { get; init; }
        public TimeSpan Delay { get; init; }
        public int RefreshCount => Volatile.Read(ref _refreshCount);

        public async Task<StoredTokens?> RefreshAsync(string refreshToken)
        {
            Interlocked.Increment(ref _refreshCount);
            if (Delay > TimeSpan.Zero) await Task.Delay(Delay);
            return Next;
        }

        public Task<AuthStatus> GetStatusAsync() => throw new NotSupportedException();
        public Task<StoredTokens> SignInAsync(string u, string p) => throw new NotSupportedException();
        public Task<SignedInUser> GetCurrentUserAsync() => throw new NotSupportedException();
        public Task EnableAuthAsync(string e, string p, string d) => throw new NotSupportedException();
        public Task DisableAuthAsync(string p) => throw new NotSupportedException();
    }
}
