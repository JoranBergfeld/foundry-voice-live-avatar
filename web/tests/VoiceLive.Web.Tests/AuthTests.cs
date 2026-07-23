using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class AuthTests : IClassFixture<TestAppFactory>
{
    private readonly TestAppFactory _factory;
    public AuthTests(TestAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Root_without_cookie_redirects_to_login()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var resp = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, resp.StatusCode);
        Assert.Equal("/login", resp.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Health_is_anonymous()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var resp = await client.GetAsync("/api/health");
        Assert.NotEqual(HttpStatusCode.Redirect, resp.StatusCode);
    }

    [Fact]
    public async Task Api_without_cookie_returns_401()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var resp = await client.GetAsync("/api/config");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
