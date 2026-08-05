using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

public sealed class TestAppFactory : WebApplicationFactory<Program>
{
    // Stable test credentials — never committed to appsettings.Development.json.
    // These are set via UseSetting to match how user-secrets would supply them in development.
    public const string TestUsername = "test-operator";
    public const string TestPassword = "test-password";

    public static string RepoConfigDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "config"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConfigDir", RepoConfigDir);
        builder.UseSetting("Auth:Username", TestUsername);
        builder.UseSetting("Auth:Password", TestPassword);
        // Supply a syntactically valid endpoint so health/config tests pass without
        // a real Azure endpoint committed to the repo. Never use a real hostname here.
        builder.UseSetting("VoiceLive:Endpoint", "https://test.voicelive.invalid");
    }
}
