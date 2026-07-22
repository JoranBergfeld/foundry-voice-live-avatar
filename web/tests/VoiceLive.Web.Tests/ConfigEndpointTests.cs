using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class ConfigEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public ConfigEndpointTests(WebApplicationFactory<Program> f)
    {
        var repoConfig = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..","..","..","..","..","..","config"));
        _factory = f.WithWebHostBuilder(b => b.UseSetting("ConfigDir", repoConfig));
    }

    [Fact]
    public async Task Health_returns_ok()
    {
        var res = await _factory.CreateClient().GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    [Fact]
    public async Task Config_returns_sanitized_client_config()
    {
        var res = await _factory.CreateClient().GetAsync("/api/config");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var body = await res.Content.ReadAsStringAsync();
        Assert.Contains("swedencentral", body);
        Assert.DoesNotContain("services.ai.azure.com", body); // endpoint must not leak to the browser
    }
}
