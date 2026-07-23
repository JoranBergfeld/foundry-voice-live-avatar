using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

public class HealthTests
{
    [Fact]
    public async Task Health_returns_ok_when_config_is_valid()
    {
        var resp = await new TestAppFactory().CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    [Fact]
    public async Task Health_returns_service_unavailable_when_config_is_invalid()
    {
        var factory = new TestAppFactory().WithWebHostBuilder(b =>
            b.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["VoiceLive:Endpoint"] = "" })));

        var resp = await factory.CreateClient().GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resp.StatusCode);
    }
}
