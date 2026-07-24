# App Service + Managed Identity Epic — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Deliver issues #1–#12 in one PR: the web app runs on Azure App Service with a system-assigned managed identity, deployed via `azd`, protected by app-level username/password login, with the CLI removed, config externalized, frontend built during publish, observability, and CI fixed.

**Architecture:** Single ASP.NET Core (.NET 10) app. Cookie auth gate in front of all endpoints. One DI `TokenCredential` shared by a DI-created WebSocket bridge. Config parsed/validated once at startup (env settings from options, show tunables from shippable `config/*.json`). Bicep provisions a fresh Foundry account/project/model-deployment + App Service + App Insights; an azd `postprovision` hook creates the persistent agent via the Foundry data-plane REST API.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, Azure.AI.VoiceLive, Azure.Identity, Azure.Monitor.OpenTelemetry.AspNetCore, esbuild/TypeScript, Bicep, azd, GitHub Actions.

**Working branch:** `feature/appservice-managed-identity-epic` (already created; spec committed).

**Baseline commands (run from repo root):**
- Build: `dotnet build web/VoiceLive.Web.sln`
- Test: `dotnet test web/VoiceLive.Web.sln`
- Frontend: `cd web/frontend && npm ci && npm run build && npx tsc --noEmit`
- Run locally: `VoiceLive__Endpoint=https://testlab-f.services.ai.azure.com VoiceLive__Mode=agent Auth__Username=op Auth__Password=pw dotnet run --project web/src/VoiceLive.Web --no-launch-profile`

**New/changed type names (keep consistent across tasks):**
- `VoiceLiveOptions` (config section `VoiceLive`): `Endpoint`, `ApiVersion`, `Mode`, `ConfigDir`, `AllowedOrigins` (`string[]`), `MaxConcurrentSessions` (`int`), `GroundingFile` (`string`).
- `AuthOptions` (config section `Auth`): `Username`, `Password`.
- `AppConfig` (validated container) with `.Server` (`ServerSessionConfig`) and `.Client` (`ClientConfig`); built by `AppConfigLoader.Load(string dir, VoiceLiveOptions env)`.
- `IVoiceLiveBridgeFactory` / `VoiceLiveBridgeFactory` (creates `VoiceLiveWebSocketBridge`).
- `SessionGate` (wraps `SemaphoreSlim`; `TryEnter()`/`Exit()`; `Active`/`Max`).

---

## PHASE 1 — Security

### Task 1: Remove the unauthenticated `/api/token` endpoint (#3)

**Files:**
- Modify: `web/src/VoiceLive.Web/Program.cs` (remove `/api/token` map + `ITokenBroker` registration + related usings)
- Delete: `web/src/VoiceLive.Web/Tokens/EntraTokenBroker.cs`, `ITokenBroker.cs`, `TokenBrokerException.cs`
- Delete: `web/tests/VoiceLive.Web.Tests/TokenEndpointTests.cs`

- [ ] **Step 1: Delete the token broker files and its test**

```bash
git rm web/src/VoiceLive.Web/Tokens/EntraTokenBroker.cs \
       web/src/VoiceLive.Web/Tokens/ITokenBroker.cs \
       web/src/VoiceLive.Web/Tokens/TokenBrokerException.cs \
       web/tests/VoiceLive.Web.Tests/TokenEndpointTests.cs
```

- [ ] **Step 2: Remove the endpoint and registration from `Program.cs`**

Delete the `builder.Services.AddSingleton<ITokenBroker, EntraTokenBroker>();` line, the entire `app.MapGet("/api/token", …)` block, and the `using VoiceLive.Web.Tokens;` line.

- [ ] **Step 3: Build and test**

Run: `dotnet test web/VoiceLive.Web.sln`
Expected: PASS (no references to removed types remain; `TokenEndpointTests` gone).

- [ ] **Step 4: Commit**

```bash
git commit -am "feat(web): remove unauthenticated /api/token endpoint (#3)"
```

---

### Task 2: Cookie authentication + login page + auth gate (#4 auth)

**Files:**
- Create: `web/src/VoiceLive.Web/Auth/AuthOptions.cs`
- Create: `web/src/VoiceLive.Web/Auth/LoginEndpoints.cs`
- Modify: `web/src/VoiceLive.Web/Program.cs`
- Modify: `web/src/VoiceLive.Web/appsettings.Development.json` (dev credentials)
- Test: `web/tests/VoiceLive.Web.Tests/AuthTests.cs`

- [ ] **Step 1: Add `AuthOptions`**

Create `Auth/AuthOptions.cs`:
```csharp
namespace VoiceLive.Web.Auth;

public sealed class AuthOptions
{
    public const string SectionName = "Auth";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public bool IsConfigured => !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
}
```

- [ ] **Step 2: Add login endpoints + constant-time check**

Create `Auth/LoginEndpoints.cs`:
```csharp
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace VoiceLive.Web.Auth;

public static class LoginEndpoints
{
    public static void MapLogin(this WebApplication app)
    {
        app.MapGet("/login", (HttpContext ctx) =>
            Results.Content(Page(ctx.Request.Query.ContainsKey("error")), "text/html"))
            .AllowAnonymous();

        app.MapPost("/login", async (HttpContext ctx, IOptions<AuthOptions> opt) =>
        {
            var form = await ctx.Request.ReadFormAsync();
            var user = form["username"].ToString();
            var pass = form["password"].ToString();
            if (Valid(opt.Value, user, pass))
            {
                var identity = new ClaimsIdentity(
                    [new Claim(ClaimTypes.Name, user)],
                    CookieAuthenticationDefaults.AuthenticationScheme);
                await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(identity));
                return Results.Redirect("/");
            }
            return Results.Redirect("/login?error=1");
        }).AllowAnonymous();

        app.MapPost("/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/login");
        });
    }

    private static bool Valid(AuthOptions o, string user, string pass)
    {
        if (!o.IsConfigured) return false;
        var u = CryptographicOperations.FixedTimeEquals(Utf8(user), Utf8(o.Username));
        var p = CryptographicOperations.FixedTimeEquals(Utf8(pass), Utf8(o.Password));
        return u && p;
    }

    private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

    private static string Page(bool error) => $$"""
        <!doctype html><html><head><meta charset="utf-8"><title>Sign in</title>
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <style>body{font-family:system-ui;display:grid;place-items:center;height:100vh;margin:0;background:#111;color:#eee}
        form{display:grid;gap:.6rem;width:16rem}input{padding:.5rem}button{padding:.5rem;cursor:pointer}
        .err{color:#f66;min-height:1.2em}</style></head>
        <body><form method="post" action="/login">
        <h2>Voice Live Avatar</h2>
        <div class="err">{{(error ? "Invalid credentials" : "")}}</div>
        <input name="username" placeholder="Username" autocomplete="username" autofocus>
        <input name="password" type="password" placeholder="Password" autocomplete="current-password">
        <button type="submit">Sign in</button></form></body></html>
        """;
}
```

- [ ] **Step 3: Wire auth + gate into `Program.cs`**

After `var builder = WebApplication.CreateBuilder(args);` add:
```csharp
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
```

After `app.UseWebSockets();` and BEFORE `app.UseDefaultFiles();`, add authentication + the gate:
```csharp
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
```

After `app.UseStaticFiles();` add `app.UseAuthorization();`. After the endpoints are mapped (before `app.Run();`) add `app.MapLogin();` and `using VoiceLive.Web.Auth;` at the top.

- [ ] **Step 4: Dev credentials**

In `appsettings.Development.json`, add:
```json
"Auth": { "Username": "operator", "Password": "rehearsal" }
```

- [ ] **Step 5: Write auth tests**

Create `web/tests/VoiceLive.Web.Tests/AuthTests.cs`:
```csharp
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

public class AuthTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    public AuthTests(WebApplicationFactory<Program> factory) => _factory = factory;

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
```
Note: `WebApplicationFactory<Program>` is already usable — the test csproj already references `Microsoft.AspNetCore.Mvc.Testing` (10.0.10) and existing tests (`ConfigEndpointTests`) use it. No package change needed.

- [ ] **Step 6: Run tests**

Run: `dotnet test web/VoiceLive.Web.sln`
Expected: PASS including the three new auth tests. (Existing `ConfigEndpointTests` may now hit auth — if they use `WebApplicationFactory` and call `/api/config`, they will get 401; update them to authenticate or mark `/api/config` handling. Adjust those tests to first POST `/login` and reuse the cookie, or move them to Task 8. If they fail, add a cookie-authenticated `HttpClient` helper.)

- [ ] **Step 7: Commit**

```bash
git commit -am "feat(web): app-level cookie auth with login page + auth gate (#4)"
```

---

### Task 3: WebSocket Origin validation + concurrent-session cap (#4)

**Files:**
- Create: `web/src/VoiceLive.Web/Session/SessionGate.cs`
- Modify: `web/src/VoiceLive.Web/Program.cs` (`/ws/session` handler)
- Test: `web/tests/VoiceLive.Web.Tests/SessionGateTests.cs`

- [ ] **Step 1: Write `SessionGate` test**

Create `SessionGateTests.cs`:
```csharp
using VoiceLive.Web.Session;
using Xunit;

public class SessionGateTests
{
    [Fact]
    public void Blocks_when_capacity_reached()
    {
        var gate = new SessionGate(2);
        Assert.True(gate.TryEnter());
        Assert.True(gate.TryEnter());
        Assert.False(gate.TryEnter());
        gate.Exit();
        Assert.True(gate.TryEnter());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test web/VoiceLive.Web.sln --filter SessionGateTests`
Expected: FAIL (SessionGate not defined).

- [ ] **Step 3: Implement `SessionGate`**

Create `Session/SessionGate.cs`:
```csharp
namespace VoiceLive.Web.Session;

public sealed class SessionGate(int max)
{
    private readonly SemaphoreSlim _slots = new(max, max);
    public int Max { get; } = max;
    public int Active => Max - _slots.CurrentCount;
    public bool TryEnter() => _slots.Wait(0);
    public void Exit() => _slots.Release();
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test web/VoiceLive.Web.sln --filter SessionGateTests`
Expected: PASS.

- [ ] **Step 5: Register the gate + add origin check + cap to `/ws/session`**

In `Program.cs`, register a singleton after auth registration:
```csharp
builder.Services.AddSingleton(sp =>
    new VoiceLive.Web.Session.SessionGate(
        sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<VoiceLive.Web.Config.VoiceLiveOptions>>().Value.MaxConcurrentSessions));
```
(If `VoiceLiveOptions` does not yet exist — it is introduced in Task 7 — temporarily use a literal `new SessionGate(2)` and switch to options in Task 7.)

Replace the `/ws/session` handler body so it: (a) rejects non-WS, (b) validates `Origin`, (c) accepts the socket, (d) enforces the cap, (e) runs the bridge inside try/finally. Origin allowlist comes from `VoiceLiveOptions.AllowedOrigins` (Task 7); until then default to same-host. Use this shape:
```csharp
app.Map("/ws/session", async (HttpContext context, SessionGate gate, IVoiceLiveBridgeFactory factory,
    IOptions<VoiceLiveOptions> opt, AppConfig appConfig) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsJsonAsync(new { error = "Expected a WebSocket request." });
        return;
    }
    if (!OriginAllowed(context, opt.Value.AllowedOrigins))
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
        await factory.Create(appConfig.Server).RunAsync(socket, context.RequestAborted);
    }
    finally
    {
        gate.Exit();
    }
});
```
Add the helper at the bottom of `Program.cs`:
```csharp
static bool OriginAllowed(HttpContext ctx, string[] allowed)
{
    var origin = ctx.Request.Headers.Origin.ToString();
    if (string.IsNullOrEmpty(origin)) return true; // non-browser client (no Origin)
    if (allowed.Length > 0 && allowed.Contains(origin, StringComparer.OrdinalIgnoreCase)) return true;
    // same-origin: Origin scheme+host[:port] equals request host
    var self = $"{ctx.Request.Scheme}://{ctx.Request.Host.Value}";
    return string.Equals(origin, self, StringComparison.OrdinalIgnoreCase);
}
```
Note: `AppConfig`, `IVoiceLiveBridgeFactory`, `VoiceLiveOptions` are introduced in Tasks 6–8. This task may be committed with a temporary inline construction (`new VoiceLiveWebSocketBridge(serverConfig, logger)` and `new SessionGate(2)`) and the DI wiring finalized in Tasks 6–8. Keep the origin check + cap logic in place now.

- [ ] **Step 6: Build + test**

Run: `dotnet test web/VoiceLive.Web.sln`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git commit -am "feat(web): WebSocket origin validation + concurrent-session cap (#4)"
```

---

### Task 4: Harden the WebSocket bridge — size cap, malformed JSON, pong, keep-alive (#5)

**Files:**
- Modify: `web/src/VoiceLive.Web/Session/VoiceLiveWebSocketBridge.cs`
- Modify: `web/src/VoiceLive.Web/Program.cs` (`UseWebSockets` keep-alive)
- Test: `web/tests/VoiceLive.Web.Tests/ControlMessageTests.cs`

- [ ] **Step 1: Add a max-message constant + size enforcement in `PumpBrowserMessagesAsync`**

In `VoiceLiveWebSocketBridge.cs`, add a field `private const int MaxMessageBytes = 1024 * 1024;`. In the receive loop, after `message.Write(buffer, 0, result.Count);` add:
```csharp
if (message.Length > MaxMessageBytes)
{
    logger.LogWarning("Browser message exceeded {Max} bytes; closing socket.", MaxMessageBytes);
    await CloseIfOpenAsync(socket, WebSocketCloseStatus.MessageTooBig, "message too big", ct);
    return;
}
```

- [ ] **Step 2: Guard `JsonDocument.Parse` in `HandleControlMessageAsync`**

Wrap the body of `HandleControlMessageAsync` so a parse failure is logged and ignored, not thrown:
```csharp
private async Task HandleControlMessageAsync(VoiceLiveSession session, string json, CancellationToken ct)
{
    JsonDocument doc;
    try { doc = JsonDocument.Parse(json); }
    catch (JsonException ex) { logger.LogDebug(ex, "Ignoring malformed control frame."); return; }
    using (doc)
    {
        if (!doc.RootElement.TryGetProperty("t", out var tProp)) return;
        switch (tProp.GetString())
        {
            // … existing cases unchanged …
            case "ping":
                await SendJsonAsync(socket, new { t = "pong" }, ct);
                break;
        }
    }
}
```
Note: the `ping` case now needs `socket` — pass `socket` into `HandleControlMessageAsync` (add a `WebSocket socket` parameter and update the call site in `PumpBrowserMessagesAsync`).

- [ ] **Step 3: Configure protocol keep-alive in `Program.cs`**

Replace `app.UseWebSockets();` with:
```csharp
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30) });
```

- [ ] **Step 4: Write control-message tests (translator seam)**

Because the bridge needs a live socket, test the parse-guard via a small static helper. Extract the "t" dispatch decision into a testable pure method `TryGetControlType(string json, out string? type)` on the bridge (static), returning `false` on malformed JSON. Add `ControlMessageTests.cs`:
```csharp
using VoiceLive.Web.Session;
using Xunit;

public class ControlMessageTests
{
    [Theory]
    [InlineData("{\"t\":\"ping\"}", true, "ping")]
    [InlineData("not json", false, null)]
    [InlineData("{\"x\":1}", true, null)]
    public void Parses_control_type(string json, bool ok, string? expected)
    {
        var result = VoiceLiveWebSocketBridge.TryGetControlType(json, out var type);
        Assert.Equal(ok, result);
        Assert.Equal(expected, type);
    }
}
```
Implement the static helper on the bridge:
```csharp
public static bool TryGetControlType(string json, out string? type)
{
    type = null;
    try
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("t", out var t)) type = t.GetString();
        return true;
    }
    catch (JsonException) { return false; }
}
```
(Use this helper inside `HandleControlMessageAsync` to avoid duplicate parsing logic where practical.)

- [ ] **Step 5: Run tests**

Run: `dotnet test web/VoiceLive.Web.sln`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git commit -am "feat(web): harden WS bridge (size cap, malformed JSON, pong, keep-alive) (#5)"
```

---

### Task 5: Production HTTP hardening (#5 item 4)

**Files:**
- Modify: `web/src/VoiceLive.Web/Program.cs`
- Modify: `web/src/VoiceLive.Web/appsettings.json` (`AllowedHosts` note stays `*`; per-env set via App Service)

- [ ] **Step 1: Add HSTS, HTTPS redirect (prod only), and security headers**

In `Program.cs`, immediately after `var app = builder.Build();`:
```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.Use(async (ctx, next) =>
{
    var h = ctx.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";
    h["X-Frame-Options"] = "DENY";
    h["Referrer-Policy"] = "no-referrer";
    h["Content-Security-Policy"] =
        "default-src 'self'; img-src 'self' data: blob:; media-src 'self' blob:; " +
        "connect-src 'self' wss: https:; script-src 'self'; style-src 'self' 'unsafe-inline'; worker-src 'self' blob:";
    await next();
});
```
Place this middleware BEFORE `app.UseAuthentication();`/static files. Verify the CSP does not break the avatar WebRTC page (it uses `blob:`/`wss:`; adjust `connect-src`/`media-src` if the browser console reports violations during smoke test).

- [ ] **Step 2: Build + test**

Run: `dotnet test web/VoiceLive.Web.sln`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git commit -am "feat(web): HSTS, HTTPS redirect, and security headers in production (#5)"
```

---

## PHASE 2 — Platform readiness

### Task 6: Single DI `TokenCredential` + DI-created bridge factory (#2)

**Files:**
- Create: `web/src/VoiceLive.Web/Session/VoiceLiveBridgeFactory.cs`
- Modify: `web/src/VoiceLive.Web/Session/VoiceLiveWebSocketBridge.cs` (ctor takes `TokenCredential`)
- Modify: `web/src/VoiceLive.Web/Program.cs`

- [ ] **Step 1: Change the bridge to accept a `TokenCredential`**

In `VoiceLiveWebSocketBridge.cs`, change the primary constructor to:
```csharp
public sealed class VoiceLiveWebSocketBridge(
    ServerSessionConfig config,
    Azure.Core.TokenCredential credential,
    ILogger<VoiceLiveWebSocketBridge> logger)
```
and in `RunAsync` replace `new DefaultAzureCredential()` with `credential`. Remove the now-unused `using Azure.Identity;` if nothing else needs it.

- [ ] **Step 2: Add the factory**

Create `Session/VoiceLiveBridgeFactory.cs`:
```csharp
using Azure.Core;
using VoiceLive.Web.Config;

namespace VoiceLive.Web.Session;

public interface IVoiceLiveBridgeFactory
{
    VoiceLiveWebSocketBridge Create(ServerSessionConfig config);
}

public sealed class VoiceLiveBridgeFactory(TokenCredential credential, ILoggerFactory loggerFactory)
    : IVoiceLiveBridgeFactory
{
    public VoiceLiveWebSocketBridge Create(ServerSessionConfig config)
        => new(config, credential, loggerFactory.CreateLogger<VoiceLiveWebSocketBridge>());
}
```

- [ ] **Step 3: Register the credential + factory in `Program.cs`**

```csharp
builder.Services.AddSingleton<Azure.Core.TokenCredential>(_ =>
{
    var clientId = builder.Configuration["AZURE_CLIENT_ID"];
    var options = new Azure.Identity.DefaultAzureCredentialOptions();
    if (!string.IsNullOrWhiteSpace(clientId)) options.ManagedIdentityClientId = clientId;
    return new Azure.Identity.DefaultAzureCredential(options);
});
builder.Services.AddSingleton<VoiceLive.Web.Session.IVoiceLiveBridgeFactory, VoiceLive.Web.Session.VoiceLiveBridgeFactory>();
```

- [ ] **Step 4: Use the factory in `/ws/session`**

Ensure the `/ws/session` handler resolves `IVoiceLiveBridgeFactory` and calls `factory.Create(serverConfig).RunAsync(...)` (finalizing the temporary construction from Task 3).

- [ ] **Step 5: Build + test**

Run: `dotnet test web/VoiceLive.Web.sln`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git commit -am "feat(web): single DI TokenCredential + DI-created bridge factory (#2)"
```

---

### Task 7: Externalize environment config into `VoiceLiveOptions` (#6 env part)

**Files:**
- Create: `web/src/VoiceLive.Web/Config/VoiceLiveOptions.cs`
- Modify: `web/src/VoiceLive.Web/appsettings.json`, `appsettings.Development.json`
- Modify: `config/session.json` (remove `endpoint`, `apiVersion`, `mode`); add `config/session.sample.json`
- Modify: `web/src/VoiceLive.Web/Program.cs` (bind options, keep `VOICELIVE_MODE` override)

- [ ] **Step 1: Add `VoiceLiveOptions`**

Create `Config/VoiceLiveOptions.cs`:
```csharp
namespace VoiceLive.Web.Config;

public sealed class VoiceLiveOptions
{
    public const string SectionName = "VoiceLive";
    public string Endpoint { get; set; } = "";
    public string ApiVersion { get; set; } = "2025-10-01";
    public string Mode { get; set; } = "model";
    public string ConfigDir { get; set; } = "config";
    public string GroundingFile { get; set; } = "grounding/company-direction.md";
    public string[] AllowedOrigins { get; set; } = [];
    public int MaxConcurrentSessions { get; set; } = 2;
}
```

- [ ] **Step 2: appsettings**

`appsettings.json` — add:
```json
"VoiceLive": {
  "Endpoint": "",
  "ApiVersion": "2025-10-01",
  "Mode": "model",
  "ConfigDir": "config",
  "GroundingFile": "grounding/company-direction.md",
  "AllowedOrigins": [],
  "MaxConcurrentSessions": 2
}
```
`appsettings.Development.json` — add a dev endpoint so local runs work:
```json
"VoiceLive": { "Endpoint": "https://testlab-f.services.ai.azure.com", "Mode": "agent" }
```

- [ ] **Step 3: Remove env values from `config/session.json`; add sample**

Edit `config/session.json` to drop `endpoint`, `apiVersion`, and `mode` (keep `region`, `model`, `voice`, and audio settings):
```json
{
  "region": "swedencentral",
  "model": "gpt-realtime",
  "voice": { "type": "azure-realtime-native", "name": "en-US-AndrewNeural" },
  "inputAudioSamplingRate": 24000,
  "inputAudioNoiseReduction": { "type": "azure_deep_noise_suppression" },
  "inputAudioEchoCancellation": { "type": "server_echo_cancellation" },
  "inputAudioTranscription": { "model": "azure-speech", "language": "en" }
}
```
Create `config/session.sample.json` with the same content plus a top comment documenting that `endpoint`/`apiVersion`/`mode` now come from app settings (`VoiceLive:Endpoint` etc.).

- [ ] **Step 4: Bind options + preserve `VOICELIVE_MODE`**

In `Program.cs`, replace the `configDir`/`envSessionMode` reads with:
```csharp
builder.Services.Configure<VoiceLive.Web.Config.VoiceLiveOptions>(
    builder.Configuration.GetSection(VoiceLive.Web.Config.VoiceLiveOptions.SectionName));
```
Read `ConfigDir` from the bound options where needed. For backward compatibility with existing tests that set top-level `ConfigDir` (e.g. `ConfigEndpointTests` uses `UseSetting("ConfigDir", …)`), bind `VoiceLiveOptions.ConfigDir` from `VoiceLive:ConfigDir` **with a fallback** to the top-level `ConfigDir` key: after `Configure<VoiceLiveOptions>(…)`, add `builder.Services.PostConfigure<VoiceLiveOptions>(o => { if (o.ConfigDir == "config" && !string.IsNullOrEmpty(builder.Configuration["ConfigDir"])) o.ConfigDir = builder.Configuration["ConfigDir"]!; });`. Keep honoring `VOICELIVE_MODE`: when resolving the mode, use `SessionModeResolver.Resolve(options.Mode, builder.Configuration["VOICELIVE_MODE"])`. (Finalized in Task 8's startup load.)

- [ ] **Step 5: Build + run smoke**

Run: `dotnet build web/VoiceLive.Web.sln`
Expected: builds (endpoint no longer in session.json; loader change lands in Task 8).

- [ ] **Step 6: Commit**

```bash
git commit -am "feat(web): externalize endpoint/mode/apiVersion into VoiceLive options (#6)"
```

---

### Task 8: Unify config loaders, load once at startup, grounding + mode-aware validation (#10, #6)

**Files:**
- Create: `web/src/VoiceLive.Web/Config/AppConfig.cs` (unified loader + `AppConfig`)
- Modify: `web/src/VoiceLive.Web/Config/WebConfig.cs` and `ServerSessionConfig.cs` (fold into the unified loader; keep the record types)
- Modify: `web/src/VoiceLive.Web/Session/VoiceLiveServiceVersionMapper.cs` (throw on unknown)
- Modify: `web/src/VoiceLive.Web/Session/VoiceLiveWebSocketBridge.cs` (instructions from config)
- Modify: `web/src/VoiceLive.Web/Program.cs` (startup load, DI singletons, `/api/config` reads singleton)
- Modify tests: `ConfigEndpointTests.cs`, `ServerSessionConfigTests.cs`

- [ ] **Step 1: Make `apiVersion` validation fail fast**

Rewrite `VoiceLiveServiceVersionMapper.Map` to throw a `WebConfigValidationException` on unknown versions instead of warning:
```csharp
public static VoiceLiveClientOptions.ServiceVersion Map(string? apiVersion)
{
    return apiVersion switch
    {
        "2025-10-01" => VoiceLiveClientOptions.ServiceVersion.V2025_10_01,
        _ => throw new VoiceLive.Web.Config.WebConfigValidationException(
            $"apiVersion '{apiVersion}' is not supported; supported: 2025-10-01.")
    };
}
```
Update the bridge call site `VoiceLiveServiceVersionMapper.Map(config.ApiVersion, …)` → `VoiceLiveServiceVersionMapper.Map(config.ApiVersion)`.

- [ ] **Step 2: Add the unified `AppConfig` loader**

Create `Config/AppConfig.cs` that parses the four files once (reusing the existing private file-record types is fine), takes a `VoiceLiveOptions env`, validates with these rules, and produces both projections:
- `endpoint`, `apiVersion`, `mode` come from `env` (validated: endpoint non-empty, apiVersion supported via mapper, mode via `SessionModeResolver`).
- `model` required only when resolved mode == `model`.
- Everything else validated as today (voice type allowlist, turntaking modes, avatar, agent, safeQuestions).
- Loads grounding instructions from `Path.Combine(dir, env.GroundingFile)`; required only in model mode.

```csharp
namespace VoiceLive.Web.Config;

public sealed record AppConfig(ServerSessionConfig Server, ClientConfig Client, string ModelInstructions);

public static class AppConfigLoader
{
    public static AppConfig Load(string dir, VoiceLiveOptions env)
    {
        var mode = SessionModeResolver.Resolve(env.Mode, Environment.GetEnvironmentVariable("VOICELIVE_MODE"));
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(env.Endpoint))
            errors.Add("VoiceLive:Endpoint: is required (set app setting VoiceLive__Endpoint)");

        // Parse show-tunable files once (reuse WebConfigLoader internal readers or inline here).
        var server = WebConfigLoader.BuildServer(dir, env, mode, errors);   // new internal builder (below)
        var client = WebConfigLoader.BuildClient(server, env, mode);

        string instructions = "";
        var groundingPath = Path.Combine(dir, env.GroundingFile);
        if (mode == SessionModeResolver.Model)
        {
            if (File.Exists(groundingPath)) instructions = File.ReadAllText(groundingPath);
            else errors.Add($"grounding: file not found at {groundingPath} (required in model mode)");
        }

        if (errors.Count > 0)
            throw new WebConfigValidationException("Configuration is invalid:\n  - " + string.Join("\n  - ", errors));
        return new AppConfig(server!, client!, instructions);
    }
}
```
Refactor `WebConfigLoader` (in `WebConfig.cs`/`ServerSessionConfig.cs`) to expose internal `BuildServer(dir, env, mode, errors)` and `BuildClient(server, env, mode)` that share ONE parse of the four files and ONE `JsonSerializerOptions`. `BuildServer` composes `ServerSessionConfig` using `env.Endpoint`, `env.ApiVersion`, `mode`, and `model` (validated per-mode). `BuildClient` projects `ClientConfig` from the parsed data (`Region`, `env.ApiVersion`, model-or-empty, voice, avatar, activeMode, agent fields, safeQuestions). Delete the now-duplicated `Load`/`LoadServerSession` public methods (or make them thin wrappers used only by tests you update). Keep the `ClientConfig`, `ServerSessionConfig`, and record types unchanged in shape.

- [ ] **Step 3: Instructions from config in the bridge**

In `VoiceLiveWebSocketBridge.RunAsync`, the model-mode branch currently passes a literal string. Add a `string modelInstructions` to the bridge constructor (supplied by the factory from `AppConfig.ModelInstructions`) and use it:
```csharp
var sessionOptions = SessionOptionsBuilder.Build(config, modelInstructions);
```
Update `IVoiceLiveBridgeFactory.Create` to accept the instructions (or pass the whole `AppConfig`). Simplest: `Create(AppConfig appConfig)` and the bridge stores `appConfig.Server` + `appConfig.ModelInstructions`.

- [ ] **Step 4: Startup load + DI singletons + `/api/config`**

In `Program.cs`, after building options, load once and register:
```csharp
var app = builder.Build();
var vlOptions = app.Services.GetRequiredService<IOptions<VoiceLiveOptions>>().Value;
AppConfig appConfig;
try { appConfig = AppConfigLoader.Load(vlOptions.ConfigDir, vlOptions); }
catch (WebConfigValidationException ex) { app.Logger.LogCritical("{Error}", ex.Message); throw; }
```
Register `appConfig` as a singleton (`builder.Services.AddSingleton(appConfig)` is not possible after Build — instead store in a captured variable and inject via closure, or register with `builder.Services.AddSingleton<AppConfig>(_ => AppConfigLoader.Load(...))` BEFORE Build and resolve it for the health check). Preferred: register a factory pre-Build:
```csharp
builder.Services.AddSingleton(sp =>
{
    var o = sp.GetRequiredService<IOptions<VoiceLiveOptions>>().Value;
    return AppConfigLoader.Load(o.ConfigDir, o);
});
```
Then `/api/config` becomes:
```csharp
app.MapGet("/api/config", (AppConfig cfg) => Results.Ok(cfg.Client));
```
and `/ws/session` uses the injected `AppConfig` (already wired in Task 3/6). Remove the per-request `WebConfigLoader.Load`/`LoadServerSession` calls and the `SendStartupErrorAsync` config-catch (config now validated at startup; keep the helper for the capacity message).

- [ ] **Step 5: Update tests**

- `ServerSessionConfigTests.cs`: construct via `AppConfigLoader.Load(dir, new VoiceLiveOptions{ Endpoint="https://x", Mode="model", ApiVersion="2025-10-01" })`; assert `.Server` fields and that missing endpoint / unknown apiVersion / model-missing-in-model-mode throw, but model may be absent in agent mode.
- `ConfigEndpointTests.cs`: authenticate first (POST `/login` with dev creds, reuse cookie) then GET `/api/config`; assert `ClientConfig` shape. Provide the app a valid `VoiceLive:Endpoint` via `WebApplicationFactory` config override.

- [ ] **Step 6: Run tests**

Run: `dotnet test web/VoiceLive.Web.sln`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git commit -am "refactor(web): unify config load+validate, grounding instructions, mode-aware validation (#10,#6)"
```

---

### Task 9: Ship `config/` in publish output (#6 deploy part)

**Files:**
- Modify: `web/src/VoiceLive.Web/VoiceLive.Web.csproj`

- [ ] **Step 1: Include repo `config/` as content copied to publish**

Add to `VoiceLive.Web.csproj` an `ItemGroup`:
```xml
<ItemGroup>
  <Content Include="..\..\..\config\**\*"
           Link="config\%(RecursiveDir)%(Filename)%(Extension)"
           CopyToOutputDirectory="PreserveNewest"
           CopyToPublishDirectory="PreserveNewest"
           Exclude="..\..\..\config\session.sample.json" />
</ItemGroup>
```

- [ ] **Step 2: Verify publish contains config**

Run: `dotnet publish web/src/VoiceLive.Web -c Release -o /tmp/pub && ls /tmp/pub/config`
Expected: `agent.json avatar.json session.json turntaking.json grounding/` present.

- [ ] **Step 3: Commit**

```bash
git commit -am "build(web): ship config/ files in publish output (#6)"
```

---

### Task 10: Build the frontend during publish; stop committing the bundle (#9)

**Files:**
- Modify: `web/src/VoiceLive.Web/VoiceLive.Web.csproj`
- Modify: `web/frontend/package.json`, `web/frontend/tsconfig.json` (add if missing)
- Untrack: `web/src/VoiceLive.Web/wwwroot/app.js` (+ `.map` if tracked)

- [ ] **Step 1: Untrack the built bundle**

```bash
git rm --cached web/src/VoiceLive.Web/wwwroot/app.js
git rm --cached web/src/VoiceLive.Web/wwwroot/app.js.map 2>/dev/null || true
```
(`.gitignore` already lists these paths; they now take effect.)

- [ ] **Step 2: package.json metadata + typecheck**

Edit `web/frontend/package.json`: set `"name": "voicelive-frontend"`, `"description": "Voice Live avatar operator UI"`, add `"private": true`, remove the `"license": "ISC"` line, replace the failing `test` script with `"typecheck": "tsc --noEmit"` (remove the `test` entry or set it to `"test": "npm run typecheck"`). Ensure a `tsconfig.json` exists with `"noEmit": true`, `"strict": true`, `"module": "esnext"`, `"target": "es2020"`, `"moduleResolution": "bundler"`; create it if missing.

- [ ] **Step 3: MSBuild target to build the bundle before publish**

Add to `VoiceLive.Web.csproj`:
```xml
<PropertyGroup>
  <FrontendDir>$(MSBuildProjectDirectory)\..\..\..\web\frontend</FrontendDir>
  <SkipFrontendBuild Condition="'$(SkipFrontendBuild)' == ''">false</SkipFrontendBuild>
</PropertyGroup>
<Target Name="BuildFrontend" BeforeTargets="Build;Publish"
        Condition="'$(SkipFrontendBuild)' != 'true'"
        Inputs="$(FrontendDir)\src\main.ts;$(FrontendDir)\src\views.ts;$(FrontendDir)\package.json;$(FrontendDir)\package-lock.json"
        Outputs="$(MSBuildProjectDirectory)\wwwroot\app.js">
  <Exec Command="npm ci" WorkingDirectory="$(FrontendDir)" />
  <Exec Command="npm run build" WorkingDirectory="$(FrontendDir)" />
</Target>
```
(Adjust the `Inputs` glob to match actual source files under `web/frontend/src`.)

- [ ] **Step 4: Verify fresh build produces the bundle**

```bash
rm -f web/src/VoiceLive.Web/wwwroot/app.js
dotnet build web/src/VoiceLive.Web
test -f web/src/VoiceLive.Web/wwwroot/app.js && echo OK
```
Expected: `OK` (bundle regenerated by the target).

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "build(web): build frontend during publish; untrack esbuild bundle (#9)"
```

---

## PHASE 3 — azd + operations

### Task 11: Observability — OpenTelemetry + real health check + session logging (#11)

**Files:**
- Modify: `web/src/VoiceLive.Web/VoiceLive.Web.csproj` (+ OTel package)
- Create: `web/src/VoiceLive.Web/Health/ConfigHealthCheck.cs`
- Modify: `web/src/VoiceLive.Web/Program.cs`
- Modify: `web/src/VoiceLive.Web/Session/VoiceLiveWebSocketBridge.cs` (session-id scope + metrics)

- [ ] **Step 1: Add the OTel package**

Add to csproj: `<PackageReference Include="Azure.Monitor.OpenTelemetry.AspNetCore" Version="1.5.0" />`.

- [ ] **Step 2: Wire Azure Monitor (only when connection string present)**

In `Program.cs`:
```csharp
if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
    builder.Services.AddOpenTelemetry().UseAzureMonitor();
```

- [ ] **Step 3: Replace static health with a real readiness check**

Create `Health/ConfigHealthCheck.cs`:
```csharp
using Microsoft.Extensions.Diagnostics.HealthChecks;
using VoiceLive.Web.Config;

namespace VoiceLive.Web.Health;

public sealed class ConfigHealthCheck(AppConfig config) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext ctx, CancellationToken ct)
        => Task.FromResult(config.Server is not null
            ? HealthCheckResult.Healthy("config loaded")
            : HealthCheckResult.Unhealthy("config missing"));
}
```
Because `AppConfig` is resolved at startup and throws on invalid config, a booted app already has valid config. To make `/api/health` reflect *config* validity without crashing the process, register `AppConfig` lazily for the health check OR catch the startup exception and expose an `AppConfig?`-holding singleton that the health check reports on. Implement a `ConfigState { AppConfig? Config; string? Error }` singleton populated at startup (try/catch), inject it into the health check (Unhealthy when `Error != null`), and have `/api/config` + `/ws/session` return 503 when `Config == null`.

In `Program.cs`:
```csharp
builder.Services.AddSingleton<ConfigState>(sp =>
{
    var o = sp.GetRequiredService<IOptions<VoiceLiveOptions>>().Value;
    try { return new ConfigState(AppConfigLoader.Load(o.ConfigDir, o), null); }
    catch (WebConfigValidationException ex) { return new ConfigState(null, ex.Message); }
});
builder.Services.AddHealthChecks().AddCheck<ConfigHealthCheck>("config");
```
Replace the health endpoint:
```csharp
app.MapHealthChecks("/api/health").AllowAnonymous();
```
Update `ConfigHealthCheck` to take `ConfigState` and report `Unhealthy(state.Error)` when set. Update `/api/config` and `/ws/session` to read `ConfigState` and return 503 / send error frame when `Config == null` (honors the user's explicit-failure preference).

- [ ] **Step 4: Per-session id log scope + metrics**

In the bridge `RunAsync`, wrap the body in a logger scope and a metrics counter:
```csharp
var sessionId = Guid.NewGuid().ToString("N")[..8];
using var scope = logger.BeginScope("session:{SessionId}", sessionId);
```
Add a static `Meter` (`"VoiceLive.Web"`) with an up/down counter `voicelive.active_sessions` (increment at start, decrement in `finally`), a histogram `voicelive.session_duration_ms`, and a counter `voicelive.errors` tagged by code (increment in the `SessionUpdateError` case). Register the meter with OTel: `.WithMetrics(m => m.AddMeter("VoiceLive.Web"))` on `AddOpenTelemetry()`.

- [ ] **Step 5: Write a health-unhealthy test**

Add to `AuthTests.cs` or a new `HealthTests.cs`: boot the factory with `VoiceLive:Endpoint` empty and assert `/api/health` returns 503. With a valid endpoint + config, assert 200.

- [ ] **Step 6: Run tests**

Run: `dotnet test web/VoiceLive.Web.sln`
Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git commit -am "feat(web): OpenTelemetry + real health check + per-session logging/metrics (#11)"
```

---

### Task 12: azure.yaml + Bicep infrastructure (#1)

**Files:**
- Create: `azure.yaml`
- Create: `infra/main.bicep`, `infra/main.parameters.json`

- [ ] **Step 1: `azure.yaml`**

Create at repo root:
```yaml
name: foundry-voice-live-avatar
metadata:
  template: foundry-voice-live-avatar@1.0.0
services:
  web:
    project: web/src/VoiceLive.Web
    language: dotnet
    host: appservice
    hooks:
      prebuild:
        posix:
          shell: sh
          run: cd web/frontend && npm ci && npm run build
        windows:
          shell: pwsh
          run: Push-Location web/frontend; npm ci; npm run build; Pop-Location
hooks:
  postprovision:
    posix:
      shell: sh
      run: ./scripts/create-agent.sh
      interactive: false
      continueOnError: true
    windows:
      shell: pwsh
      run: ./scripts/create-agent.ps1
      interactive: false
      continueOnError: true
```

- [ ] **Step 2: `infra/main.bicep`**

Create subscription-scoped Bicep. Full content (from the research, apiVersions pinned):
```bicep
targetScope = 'subscription'

@minLength(1) @description('Primary location') param location string = 'swedencentral'
@minLength(1) param environmentName string
@description('App login username') param authUsername string
@secure() @description('App login password') param authPassword string
@description('Voice Live mode: model or agent') param voiceLiveMode string = 'agent'
@description('Voice Live API version') param apiVersion string = '2025-10-01'
@description('Linux runtime; empty for self-contained deploy') param linuxFxVersion string = 'DOTNETCORE|10.0'
param resourceGroupName string = 'rg-${environmentName}'

var token = uniqueString(subscription().id, environmentName, location)
var tags = { 'azd-env-name': environmentName }

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: tags
}

module resources 'resources.bicep' = {
  scope: rg
  name: 'resources'
  params: {
    location: location
    token: token
    tags: tags
    authUsername: authUsername
    authPassword: authPassword
    voiceLiveMode: voiceLiveMode
    apiVersion: apiVersion
    linuxFxVersion: linuxFxVersion
    environmentName: environmentName
  }
}

output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
output SERVICE_WEB_NAME string = resources.outputs.webAppName
output SERVICE_WEB_URI string = resources.outputs.webAppUri
output AZURE_AI_SERVICES_NAME string = resources.outputs.aiServicesName
output AZURE_AI_PROJECT_NAME string = resources.outputs.projectName
output AZURE_AI_PROJECT_ENDPOINT string = resources.outputs.projectEndpoint
```

- [ ] **Step 3: `infra/resources.bicep`** (resource-group scope) — account, project, deployment, plan, site, insights, roles

```bicep
param location string
param token string
param tags object
param authUsername string
@secure() param authPassword string
param voiceLiveMode string
param apiVersion string
param linuxFxVersion string
param environmentName string

var aiName = 'ai${token}'
var projectName = 'proj-default'

resource ai 'Microsoft.CognitiveServices/accounts@2025-06-01' = {
  name: aiName
  location: location
  tags: tags
  kind: 'AIServices'
  sku: { name: 'S0' }
  identity: { type: 'SystemAssigned' }
  properties: {
    allowProjectManagement: true
    customSubDomainName: aiName
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
  }
}

resource project 'Microsoft.CognitiveServices/accounts/projects@2025-06-01' = {
  parent: ai
  name: projectName
  location: location
  identity: { type: 'SystemAssigned' }
  properties: { displayName: 'Voice Live Avatar' }
}

resource agentModel 'Microsoft.CognitiveServices/accounts/deployments@2025-06-01' = {
  parent: ai
  name: 'gpt-4o-mini'
  sku: { name: 'GlobalStandard', capacity: 30 }
  properties: {
    model: { format: 'OpenAI', name: 'gpt-4o-mini', version: '2024-07-18' }
    versionUpgradeOption: 'OnceCurrentVersionExpired'
  }
}

resource logs 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: 'log-${token}'
  location: location
  tags: tags
  properties: { sku: { name: 'PerGB2018' }, retentionInDays: 30 }
}

resource appi 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${token}'
  location: location
  tags: tags
  kind: 'web'
  properties: { Application_Type: 'web', WorkspaceResourceId: logs.id }
}

resource plan 'Microsoft.Web/serverfarms@2024-11-01' = {
  name: 'plan-${token}'
  location: location
  tags: tags
  kind: 'linux'
  sku: { name: 'B1' }
  properties: { reserved: true }
}

resource site 'Microsoft.Web/sites@2024-11-01' = {
  name: 'app-${token}'
  location: location
  tags: union(tags, { 'azd-service-name': 'web' })
  kind: 'app,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: linuxFxVersion
      webSocketsEnabled: true
      alwaysOn: true
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      http20Enabled: true
      healthCheckPath: '/api/health'
      appSettings: [
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appi.properties.ConnectionString }
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'ConfigDir', value: 'config' }
        { name: 'VoiceLive__ConfigDir', value: 'config' }
        { name: 'VoiceLive__Endpoint', value: ai.properties.endpoint }
        { name: 'VoiceLive__Mode', value: voiceLiveMode }
        { name: 'VoiceLive__ApiVersion', value: apiVersion }
        { name: 'VoiceLive__AllowedOrigins__0', value: 'https://app-${token}.azurewebsites.net' }
        { name: 'Auth__Username', value: authUsername }
        { name: 'Auth__Password', value: authPassword }
      ]
    }
  }
}

var cognitiveServicesUser = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908')
var foundryUser = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '53ca6127-db72-4b80-b1b0-d745d6d5456d')

resource raCog 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: ai
  name: guid(ai.id, site.id, 'cog-user')
  properties: { principalId: site.identity.principalId, principalType: 'ServicePrincipal', roleDefinitionId: cognitiveServicesUser }
}

resource raProj 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  scope: project
  name: guid(project.id, site.id, 'foundry-user')
  properties: { principalId: site.identity.principalId, principalType: 'ServicePrincipal', roleDefinitionId: foundryUser }
}

output webAppName string = site.name
output webAppUri string = 'https://${site.properties.defaultHostName}'
output aiServicesName string = ai.name
output projectName string = project.name
output projectEndpoint string = 'https://${aiName}.services.ai.azure.com/api/projects/${projectName}'
```

- [ ] **Step 4: `infra/main.parameters.json`** (azd-style with env substitution)

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "environmentName": { "value": "${AZURE_ENV_NAME}" },
    "location": { "value": "${AZURE_LOCATION}" },
    "authUsername": { "value": "${AUTH_USERNAME}" },
    "authPassword": { "value": "${AUTH_PASSWORD}" },
    "voiceLiveMode": { "value": "${VOICELIVE_MODE=agent}" },
    "linuxFxVersion": { "value": "${LINUX_FX_VERSION=DOTNETCORE|10.0}" }
  }
}
```

- [ ] **Step 5: Move the resources.bicep reference**

Adjust Task-12 Step-2 `main.bicep` module path to `'resources.bicep'` (same `infra/` dir). Rename the module file accordingly (`infra/resources.bicep`).

- [ ] **Step 6: Lint/build the Bicep**

Run: `az bicep build --file infra/main.bicep`
Expected: compiles with no errors (warnings about role assignments acceptable).

- [ ] **Step 7: Commit**

```bash
git add azure.yaml infra/
git commit -m "feat(infra): azure.yaml + Bicep for App Service, Foundry, App Insights (#1)"
```

---

### Task 13: Agent provisioning via postprovision hook (#1)

**Files:**
- Create: `scripts/create-agent.sh`, `scripts/create-agent.ps1`

- [ ] **Step 1: VERIFY the data-plane path live before writing the call**

Run against the existing project (do NOT fabricate — confirm the real path):
```bash
az rest --method GET --resource https://ai.azure.com \
  --url "https://testlab-f.services.ai.azure.com/api/projects/proj-default/assistants?api-version=v1" -o json | head -c 300
# If 404, try:
az rest --method GET --resource https://ai.azure.com \
  --url "https://testlab-f.services.ai.azure.com/api/projects/proj-default/agents?api-version=v1" -o json | head -c 300
```
Record which path returns 200. Use that path in the script; keep the other as the documented fallback. If neither can be confirmed, the script must print a clear manual-step message and exit 0 (agent creation becomes a documented manual step — never a fabricated call).

- [ ] **Step 2: `scripts/create-agent.sh`** (idempotent GET-then-create)

```bash
#!/usr/bin/env bash
set -euo pipefail
: "${AZURE_AI_SERVICES_NAME:?}"; : "${AZURE_AI_PROJECT_NAME:?}"
ENDPOINT="https://${AZURE_AI_SERVICES_NAME}.services.ai.azure.com/api/projects/${AZURE_AI_PROJECT_NAME}"
API="v1"; RES="https://ai.azure.com"; NAME="company-direction-avatar"
PATHSEG="${FOUNDRY_AGENT_PATH:-assistants}"   # verified in Step 1

echo "Listing agents at ${ENDPOINT}/${PATHSEG}"
EXISTING=$(az rest --method GET --resource "$RES" \
  --url "${ENDPOINT}/${PATHSEG}?api-version=${API}" -o json 2>/dev/null || echo '{}')
ID=$(echo "$EXISTING" | jq -r --arg n "$NAME" '.data[]? | select(.name==$n) | .id' | head -n1)

if [ -z "$ID" ] || [ "$ID" = "null" ]; then
  INSTR=$(cat config/grounding/company-direction.md 2>/dev/null || echo "You are the on-stage avatar assistant. Answer concisely.")
  BODY=$(jq -n --arg m gpt-4o-mini --arg n "$NAME" --arg i "$INSTR" \
    '{model:$m, name:$n, instructions:$i}')
  RESP=$(az rest --method POST --resource "$RES" \
    --url "${ENDPOINT}/${PATHSEG}?api-version=${API}" \
    --headers "Content-Type=application/json" --body "$BODY")
  ID=$(echo "$RESP" | jq -r '.id')
  echo "Created agent ${ID}"
else
  echo "Reusing agent ${ID}"
fi
azd env set FOUNDRY_AGENT_ID "$ID" >/dev/null 2>&1 || true
```
Create an equivalent `scripts/create-agent.ps1`. Make both executable (`chmod +x scripts/create-agent.sh`).

- [ ] **Step 3: Commit**

```bash
git add scripts/
git commit -m "feat(infra): postprovision hook to create the Foundry persistent agent (#1)"
```

---

### Task 14: Fix CI — move to `.github/workflows/`, npm ci + typecheck, drop cli (#7)

**Files:**
- Create: `.github/workflows/ci.yml`
- Delete: `pipeline/ci.yml` (and the `pipeline/` dir)

- [ ] **Step 1: Create the workflow**

`.github/workflows/ci.yml`:
```yaml
name: CI
on: [push, pull_request]
jobs:
  web:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: 10.0.x }
      - uses: actions/setup-node@v4
        with: { node-version: 24 }
      - run: dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true
  frontend:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: 24 }
      - run: npm ci
        working-directory: web/frontend
      - run: npx tsc --noEmit
        working-directory: web/frontend
      - run: npm run build
        working-directory: web/frontend
```

- [ ] **Step 2: Remove the old pipeline dir**

```bash
git rm pipeline/ci.yml
```

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "ci: run on GitHub Actions (web tests + frontend typecheck/build); drop cli job (#7)"
```

---

## PHASE 4 — Consolidation

### Task 15: Remove the CLI (#8)

**Files:**
- Delete: `cli/` (entire directory)

- [ ] **Step 1: Remove**

```bash
git rm -r cli/
```

- [ ] **Step 2: Verify nothing references it**

Run: `grep -rn "VoiceLive.Cli\|cli/" --include=*.cs --include=*.csproj --include=*.sln . ; grep -rn "cli/" .github/ azure.yaml infra/ || true`
Expected: no build references (docs references handled in Task 16).

- [ ] **Step 3: Build + test**

Run: `dotnet test web/VoiceLive.Web.sln`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git commit -am "chore: remove deprecated CLI in favor of the web app (#8)"
```

---

### Task 16: Documentation sweep + Deploy section (#8, #3, #4, #5, #1)

**Files:**
- Modify: root `README.md`, `web/README.md`, `docs/runbook.md`, `docs/rehearsal-checklist.md`, `docs/config-schema.md`, `config/grounding/company-direction.md`

- [ ] **Step 1: Root `README.md`**

Remove the "two independent apps" framing and the phantom `/tools/sync-agent` reference. Describe the single web app, the App Service + managed identity + azd deployment model, and the app-level login trust model. Add a "Deploy" section:
```markdown
## Deploy (azd)
Prerequisites: Azure CLI + azd, an Azure subscription. No Entra app registration required.
1. `az login && azd auth login`
2. `azd env new <name>`; `azd env set AZURE_LOCATION swedencentral`
3. `azd env set AUTH_USERNAME <user>`; `azd env set AUTH_PASSWORD <password>`
4. `azd up`  # provisions Foundry (account/project/gpt-4o-mini) + App Service + App Insights, builds the
             # frontend (prebuild), creates the agent (postprovision), and deploys the app.
5. Browse the printed URL, sign in, and start a session.
If `DOTNETCORE|10.0` is unavailable in your region, set `azd env set LINUX_FX_VERSION ""` and deploy
self-contained (see docs/runbook.md).
```

- [ ] **Step 2: `web/README.md`**

Update the endpoint list (remove `/api/token`; note `/login`,`/logout`; `/api/health` is a real health check), and rewrite the Security section (cookie login, origin check, session cap, rate limiting, HSTS/headers).

- [ ] **Step 3: runbook / rehearsal-checklist / config-schema**

Remove every instruction to run the CLI; point rehearsal at the browser operator view + `azd`/local `dotnet run`. In `docs/config-schema.md`, note `endpoint`/`apiVersion`/`mode` moved to app settings (`VoiceLive:*`) and `model` is required only in model mode. Remove the `tools/sync-agent` reference in `config/grounding/company-direction.md` (replace with "Instructions are applied to the agent at provisioning time by `scripts/create-agent.sh`, and used directly in model mode.").

- [ ] **Step 4: Commit**

```bash
git commit -am "docs: web-only docs, new trust model, azd Deploy section (#8,#1,#3,#4,#5)"
```

---

### Task 17: Full local verification pass

- [ ] **Step 1: Build + test + frontend + bicep**

```bash
dotnet build web/VoiceLive.Web.sln
dotnet test web/VoiceLive.Web.sln
cd web/frontend && npm ci && npx tsc --noEmit && npm run build && cd ../../..
az bicep build --file infra/main.bicep
```
Expected: all green.

- [ ] **Step 2: Local run smoke**

```bash
VoiceLive__Endpoint=https://testlab-f.services.ai.azure.com VoiceLive__Mode=agent \
Auth__Username=op Auth__Password=pw \
dotnet run --project web/src/VoiceLive.Web --no-launch-profile &
sleep 8
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5xxx/api/health   # 200
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5xxx/api/config    # 401 (needs login)
```
(Use the port from launch output; kill the process by PID afterward.)

- [ ] **Step 3: Commit any fixes; update plan/spec checkboxes**

---

### Task 18: Live deploy via `azd up` + smoke test (needs the user's subscription)

- [ ] **Step 1: Confirm subscription + login**

`az account set --subscription <SUB_ID>`; `azd auth login`.

- [ ] **Step 2: `azd up`**

Provision + deploy. If the .NET 10 runtime is rejected, set `LINUX_FX_VERSION=""` and re-deploy self-contained (`dotnet publish -r linux-x64 --self-contained`), then `azd deploy`.

- [ ] **Step 3: Verify the postprovision agent path** using the newly-created project endpoint (Task 13 Step 1 logic), confirm the agent exists.

- [ ] **Step 4: Browser smoke**

Sign in at the site URL, confirm `/api/health` 200, `/api/config` after login, and a full `/ws/session` avatar session in agent mode. Capture App Insights shows traces.

- [ ] **Step 5: Push branch + open the PR** closing #1–#12.

---

## Self-review notes (coverage)

- #1 → Tasks 12,13,18; #2 → Task 6; #3 → Task 1; #4 → Tasks 2,3; #5 → Tasks 4,5; #6 → Tasks 7,8,9;
  #7 → Task 14; #8 → Tasks 15,16; #9 → Task 10; #10 → Task 8; #11 → Task 11; #12 → whole PR.
- Ordering note: Task 3 uses types from Tasks 6–8; it lands the origin/cap logic with temporary wiring
  and Tasks 6–8 finalize DI. Keep the branch building green after each task (run `dotnet test`).
- Risks tracked in the spec §10 (agent REST path verified live in Task 13 Step 1; .NET 10 fallback in
  Tasks 12/18).
