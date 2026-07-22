using VoiceLive.Web.Config;
using VoiceLive.Web.Tokens;

var builder = WebApplication.CreateBuilder(args);
var configDir = builder.Configuration["ConfigDir"] ?? "config";
builder.Services.AddSingleton<ITokenBroker, EntraTokenBroker>();
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

app.MapGet("/api/token", async (ITokenBroker broker, CancellationToken ct) =>
{
    try
    {
        var token = await broker.GetTokenAsync(ct);
        return Results.Ok(new { token = token.Token, expiresOn = token.ExpiresOn });
    }
    catch (TokenBrokerException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 502);
    }
});

app.Run();

public partial class Program { }
