using System.Net.WebSockets;
using VoiceLive.Web.Auth;
using VoiceLive.Web.Config;
using VoiceLive.Web.Session;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<VoiceLive.Web.Auth.AuthOptions>(
    builder.Configuration.GetSection(VoiceLive.Web.Auth.AuthOptions.SectionName));
builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/login";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();
var configDir = builder.Configuration["ConfigDir"] ?? "config";
var envSessionMode = builder.Configuration["VOICELIVE_MODE"];
builder.Services.AddSingleton(new VoiceLive.Web.Session.SessionGate(2));
var app = builder.Build();

app.UseWebSockets();

app.UseAuthentication();
app.Use(async (ctx, next) =>
{
    var path = ctx.Request.Path;
    var anon = path.StartsWithSegments("/login")
        || path.StartsWithSegments("/logout")
        || path.Equals("/api/health", StringComparison.OrdinalIgnoreCase);
    if (!anon && !(ctx.User.Identity?.IsAuthenticated ?? false))
    {
        if (path.StartsWithSegments("/ws") || path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }
        ctx.Response.Redirect("/login");
        return;
    }
    await next();
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthorization();

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

app.Map("/ws/session", async (HttpContext context, SessionGate gate, ILogger<VoiceLiveWebSocketBridge> logger) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = "Expected a WebSocket request." });
        return;
    }
    if (!OriginAllowed(context, Array.Empty<string>()))
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return;
    }
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    if (!gate.TryEnter())
    {
        await SendStartupErrorAsync(socket, "The server is at capacity. Try again shortly.", context.RequestAborted);
        return;
    }
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
    finally
    {
        gate.Exit();
    }
});

app.MapLogin();

static async Task SendStartupErrorAsync(WebSocket socket, string message, CancellationToken ct)
{
    if (socket.State == WebSocketState.Open)
    {
        var bytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new { t = "error", message });
        await socket.SendAsync(bytes, WebSocketMessageType.Text, WebSocketMessageFlags.EndOfMessage, ct);
        await socket.CloseAsync(WebSocketCloseStatus.InternalServerError, "configuration failed", ct);
    }
}

static bool OriginAllowed(HttpContext ctx, string[] allowed)
{
    var origin = ctx.Request.Headers.Origin.ToString();
    if (string.IsNullOrEmpty(origin)) return true; // non-browser client (no Origin)
    if (allowed.Length > 0 && allowed.Contains(origin, StringComparer.OrdinalIgnoreCase)) return true;
    // same-origin: Origin scheme+host[:port] equals request host
    var self = $"{ctx.Request.Scheme}://{ctx.Request.Host.Value}";
    return string.Equals(origin, self, StringComparison.OrdinalIgnoreCase);
}

app.Run();

public partial class Program { }
