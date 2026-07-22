using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using VoiceLive.Web.Tokens;
using Xunit;

public class TokenEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public TokenEndpointTests(WebApplicationFactory<Program> f) => _factory = f;

    private sealed class FakeOk : ITokenBroker
    { public Task<AccessTokenResult> GetTokenAsync(CancellationToken ct)
        => Task.FromResult(new AccessTokenResult("faketoken", DateTimeOffset.UtcNow.AddMinutes(30))); }

    private sealed class FakeFail : ITokenBroker
    { public Task<AccessTokenResult> GetTokenAsync(CancellationToken ct)
        => throw new TokenBrokerException("No Azure credential available"); }

    [Fact]
    public async Task Returns_token_when_broker_ok()
    {
        var client = _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.AddSingleton<ITokenBroker, FakeOk>())).CreateClient();
        var res = await client.GetAsync("/api/token");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains("faketoken", await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Returns_502_with_clear_message_when_no_credential()
    {
        var client = _factory.WithWebHostBuilder(b => b.ConfigureServices(s =>
            s.AddSingleton<ITokenBroker, FakeFail>())).CreateClient();
        var res = await client.GetAsync("/api/token");
        Assert.Equal(HttpStatusCode.BadGateway, res.StatusCode);
        Assert.Contains("credential", await res.Content.ReadAsStringAsync());
    }
}
