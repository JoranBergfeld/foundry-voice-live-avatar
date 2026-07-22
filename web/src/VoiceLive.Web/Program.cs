using VoiceLive.Web.Config;

var builder = WebApplication.CreateBuilder(args);
var configDir = builder.Configuration["ConfigDir"] ?? "config";
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/config", () =>
{
    try
    {
        var clientConfig = WebConfigLoader.Load(configDir);
        return Results.Ok(clientConfig);
    }
    catch (WebConfigValidationException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 500);
    }
});

app.Run();

public partial class Program { }
