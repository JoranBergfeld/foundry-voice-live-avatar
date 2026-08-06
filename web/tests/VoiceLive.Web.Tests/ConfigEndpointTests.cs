using System.Net;
using Xunit;

public class ConfigEndpointTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;
    public ConfigEndpointTests(TestAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_returns_ok()
    {
        var res = await _factory.CreateClient().GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Config_returns_sanitized_client_config()
    {
        var client = await AuthedClientAsync();
        var res = await client.GetAsync("/api/config");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("swedencentral", body);
        Assert.DoesNotContain("services.ai.azure.com", body); // endpoint must not leak to the browser
    }

    private async Task<HttpClient> AuthedClientAsync()
    {
        var client = _factory.CreateClient();
        var form = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("username", TestAppFactory.TestUsername),
            new KeyValuePair<string, string>("password", TestAppFactory.TestPassword),
        });
        var login = await client.PostAsync("/login", form);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }
}
