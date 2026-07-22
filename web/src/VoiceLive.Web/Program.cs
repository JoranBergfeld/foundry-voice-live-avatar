using System.Net.WebSockets;
using VoiceLive.Web.Config;
using VoiceLive.Web.Tokens;
using VoiceLive.Web.Session;

var builder = WebApplication.CreateBuilder(args);
var configDir = builder.Configuration["ConfigDir"] ?? "config";
var envSessionMode = builder.Configuration["VOICELIVE_MODE"];
builder.Services.AddSingleton<ITokenBroker, EntraTokenBroker>();
var app = builder.Build();

app.UseWebSockets();

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

app.Map("/ws/session", async (HttpContext context, ILogger<VoiceLiveWebSocketBridge> logger) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = "Expected a WebSocket request." });
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    try
    {
        var loaded = WebConfigLoader.LoadServerSession(configDir);
        var serverConfig = loaded with { Mode = SessionModeResolver.Resolve(loaded.Mode, envSessionMode) };
        await new VoiceLiveWebSocketBridge(serverConfig, logger).RunAsync(socket, context.RequestAborted);
    }
    catch (WebConfigValidationException ex)
    {
        await SendStartupErrorAsync(socket, ex.Message, context.RequestAborted);
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

static async Task SendStartupErrorAsync(WebSocket socket, string message, CancellationToken ct)
{
    if (socket.State == WebSocketState.Open)
    {
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { t = "error", message });
        await socket.SendAsync(bytes, WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, ct);
        await socket.CloseAsync(WebSocketCloseStatus.InternalServerError, "configuration failed", ct);
    }
}

app.Run();

public partial class Program { }
