using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

public sealed class TestAppFactory : WebApplicationFactory<Program>
{
    public static string RepoConfigDir =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "config"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
        => builder.UseSetting("ConfigDir", RepoConfigDir);
}
