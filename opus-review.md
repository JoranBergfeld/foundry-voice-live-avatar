# Implementation Review — `foundry-voice-live-avatar`

**Reviewer:** Claude Opus 5 (GitHub Copilot CLI)
**Date:** 2026-08-05
**Commit reviewed:** `d5110dc` (`docs: add MIT License file`)
**Scope:** entire repository — ASP.NET Core 10 server, TypeScript browser client, AudioWorklet, Bicep infrastructure, CI, configuration and documentation.

---

## 1. Executive summary

This is a well-structured, thoughtfully engineered project. The architecture is sound: the browser client is genuinely thin, all Azure credentials stay server-side, session lifetime is owned by the server, and avatar media bypasses the app entirely over WebRTC. Configuration validation is unusually rigorous, cancellation and teardown are handled with real care on both sides of the wire, and the "invalid config → unhealthy but still running" pattern is a mature choice.

Verification performed during this review:

| Check | Result |
|---|---|
| `dotnet test web/VoiceLive.Web.sln` | **90/90 passing** |
| `dotnet list package --vulnerable --include-transitive` | **No vulnerable packages** |
| `npm audit` (frontend) | **0 vulnerabilities** |
| `dotnet list package --outdated` | 1 minor (`Azure.Monitor.OpenTelemetry.AspNetCore` 1.5.0 → 1.6.0) |
| `npm outdated` (frontend) | 1 patch (`@playwright/test` 1.62.0 → 1.62.1) |

The dependency posture is clean and the test suite is green. The findings below are therefore about **design-level gaps** rather than rot.

The most important issues are concentrated in three areas:

1. **Authentication is the weakest link.** A single static username/password, committed dev credentials, a plaintext password in App Service settings, no CSRF protection, and a rate limiter that is trivially bypassable behind the proxy configuration the Bicep template itself enables.
2. **The `say` control frame is an unrestricted prompt-injection and cost channel.** Any authenticated client can override the grounding system prompt with arbitrary text, with no length cap and no rate limit.
3. **There is no session lifetime bound.** With `MaxConcurrentSessions` defaulting to 2 and no idle or absolute timeout, two idle browser tabs can permanently deny service *and* hold two billed Azure Voice Live + avatar rendering sessions open indefinitely.

**Overall assessment: solid foundation, not yet ready for untrusted or internet-facing users.** For the stated "stage/rehearsal kiosk behind a shared password" use case it is broadly fit for purpose, but findings S1–S6 and P1 should be addressed before the app is exposed beyond a controlled event.

---

## 2. Severity key

| Level | Meaning |
|---|---|
| **Critical** | Exploitable or service-affecting; fix before any exposed deployment. |
| **High** | Material security, cost or correctness risk; fix soon. |
| **Medium** | Real defect or notable gap; schedule it. |
| **Low** | Polish, consistency, maintainability. |

---

## 3. Security findings

### S1 — Login rate limiter is bypassable via spoofed `X-Forwarded-For` — **Critical**

**Where:** `web/src/VoiceLive.Web/Program.cs` (rate limiter), `infra/resources.bicep` (`ASPNETCORE_FORWARDEDHEADERS_ENABLED`)

The limiter partitions on the client IP:

```csharp
partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
```

`infra/resources.bicep` sets `ASPNETCORE_FORWARDEDHEADERS_ENABLED = 'true'`. That built-in path enables `ForwardedHeaders` middleware **without** restricting `KnownProxies`/`KnownNetworks`, so ASP.NET Core will rewrite `RemoteIpAddress` from whatever `X-Forwarded-For` the caller supplies. An attacker rotating that header gets a fresh 5-requests-per-minute bucket per value — effectively unlimited attempts.

This compounds with **S2**: there is exactly one credential pair, chosen by a human, with no lockout, no MFA, no per-account throttle and no delay. The 5/min limiter documented in `README.md` as a security control is the *only* brute-force defence, and it does not hold.

Additionally, the fallback partition key `"unknown"` means that if forwarded headers are ever *disabled* (any non-App-Service host — Container Apps, Docker, self-hosted, or the test harness), **every** client collapses into a single shared bucket and 5 total logins per minute are permitted across all users. The rate limiter is therefore either bypassable or globally throttling, depending on host — never correct.

**Recommendation:** Configure `ForwardedHeadersOptions` explicitly in code with `KnownNetworks`/`KnownProxies` set to the actual front end, rather than relying on the env-var shortcut. Partition on the *validated* remote IP. Add a second, coarser global limiter on `/login` as a backstop, plus exponential backoff or a lockout after repeated failures.

---

### S2 — Credentials committed to the repository — **Critical**

**Where:** `web/src/VoiceLive.Web/appsettings.Development.json`

```json
"Auth": { "Username": "operator", "Password": "rehearsal" },
"VoiceLive": { "Endpoint": "https://testlab-f.services.ai.azure.com", "Mode": "agent" }
```

Two problems:

- A working username/password pair is in version control and is additionally published in `README.md` line 243. Anyone who deploys with `ASPNETCORE_ENVIRONMENT=Development`, or who forgets to override `Auth__*`, ships a publicly known credential. Because `appsettings.Development.json` layers *over* `appsettings.json`, this is a real "works by accident in production" hazard.
- The endpoint `https://testlab-f.services.ai.azure.com` looks like a real tenant-specific Azure AI Services hostname. That is an information disclosure about internal infrastructure and it will silently mis-target a developer's local session.

**Recommendation:** Move both to `dotnet user-secrets` (`UserSecretsId` in the `.csproj`). Leave `appsettings.Development.json` with only non-sensitive logging overrides. Rotate the `testlab-f` resource if it is real. Replace the README's published defaults with instructions to set user secrets.

---

### S3 — App password stored as a plaintext App Service app setting — **High**

**Where:** `infra/resources.bicep`

```bicep
{ name: 'Auth__Password', value: authPassword }
```

The Bicep parameter is correctly marked `@secure()`, which protects it in deployment *inputs* — but the value then lands as an ordinary App Service application setting. It is readable by any principal with `Microsoft.Web/sites/config/list/action` (that includes Contributor and Website Contributor), visible in the portal, and returned by `az webapp config appsettings list`. `@secure()` gives a false sense of protection here.

**Recommendation:** Provision a Key Vault, store the password as a secret, grant the site's system-assigned identity `Key Vault Secrets User`, and set the app setting to a Key Vault reference (`@Microsoft.KeyVault(SecretUri=...)`). Given the site already has a managed identity and RBAC assignments, this is a small increment.

---

### S4 — No CSRF/antiforgery protection on `POST /login` and `POST /logout` — **High**

**Where:** `web/src/VoiceLive.Web/Auth/LoginEndpoints.cs`

Both endpoints read the form directly via `ctx.Request.ReadFormAsync()`. Minimal APIs only auto-apply antiforgery validation when a handler binds `[FromForm]`, so no token is issued or checked here.

`SameSite=Lax` blocks the auth cookie on cross-site POSTs, which substantially mitigates *logout* CSRF. It does **not** mitigate **login CSRF**, because that attack requires no existing cookie: a third-party page can silently POST attacker-controlled credentials and authenticate the victim's browser as the attacker's session — a classic vector for feeding poisoned transcripts and tool activity into a session the attacker controls, or for session-fixation-style confusion on a shared kiosk.

**Recommendation:** `builder.Services.AddAntiforgery()`, emit a token in the login form, and validate on POST (or bind the form model with `[FromForm]` so the framework validates automatically). Add `form-action 'self'` to the CSP (see S8).

---

### S5 — `say` control frame is an unrestricted prompt-injection and cost channel — **High**

**Where:** `web/src/VoiceLive.Web/Session/VoiceLiveWebSocketBridge.cs`, `HandleControlMessageAsync`

```csharp
case "say":
    if (doc.RootElement.TryGetProperty("text", out var text))
    {
        var prompt = text.GetString();
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            if (config.Mode == "agent") { await session.AddItemAsync(new UserMessageItem(prompt), ct); ... }
            else { await session.StartResponseAsync(prompt, ct); }
        }
    }
```

The UI only ever sends server-supplied `safeQuestions` and a fixed "repeat" string — but the server accepts **any** text from **any** client that holds a session cookie (a browser console, `websocat`, a compromised kiosk tab). Consequences:

- **Model mode:** `StartResponseAsync(prompt)` supplies *per-response instructions*. A client can therefore override the carefully curated grounding prompt from `config/grounding/company-direction.md` and make the on-stage avatar say arbitrary things. For a public-facing presentation avatar, this is a reputational risk, not a theoretical one.
- **Agent mode:** the text is injected as a user turn — same outcome, one layer removed.
- **No length cap.** `MaxMessageBytes` bounds the *frame* at 1 MB, so a ~1 MB instruction blob is accepted and forwarded to Azure verbatim.
- **No rate limit.** The 1/min login limiter does not apply to WebSocket frames. A loop of `say` frames drives unbounded Azure inference and avatar-rendering spend.

**Recommendation:** Treat `say` as privileged. Either (a) validate the text against the server's own `config.Agent.SafeQuestions` allow-list plus the fixed repeat prompt, or (b) replace the free-text field with an index into `safeQuestions` so no client-authored text ever reaches the model. In both cases add a hard length cap and a per-connection token bucket. If free text must stay for operator use, gate it behind a distinct role/claim rather than plain authentication.

---

### S6 — No idle or absolute timeout on `/ws/session`; capacity gate is trivially exhausted — **High**

**Where:** `web/src/VoiceLive.Web/Program.cs`, `Session/SessionGate.cs`, `Config/VoiceLiveOptions.cs`

`MaxConcurrentSessions` defaults to `2`. `SessionGate.TryEnter()` reserves a slot for the *entire* lifetime of the WebSocket, and nothing bounds that lifetime:

- No absolute session cap.
- No idle timeout. The client pings every 25 s and `WebSocketOptions.KeepAliveInterval` is 30 s, so a socket that is merely *open* stays healthy forever.
- No cap on total audio bytes or control frames per connection.

Two browser tabs left open — accidentally or deliberately — permanently exhaust capacity for everyone, **and** hold two billed Azure Voice Live sessions with avatar rendering open indefinitely. For a kiosk/stage app this is both a denial-of-service and a runaway-cost path. Note the gate is also per-instance, so the cap is not global if the app is ever scaled beyond one instance.

**Recommendation:** Add an absolute session timeout (e.g. 30–60 min, configurable) and an inactivity timeout keyed on genuine user activity — audio frames or turn events, deliberately *not* `ping`, which a zombie tab keeps sending. Link both into the existing `CancellationTokenSource`, which already propagates cleanly to both pumps. Consider a per-connection cap on cumulative audio bytes. Emit a metric when the gate rejects, so exhaustion is observable.

---

### S7 — WebSocket origin check passes when `Origin` is absent — **Medium**

**Where:** `web/src/VoiceLive.Web/Program.cs`, `OriginAllowed`

```csharp
if (string.IsNullOrEmpty(origin)) return true; // non-browser client (no Origin)
```

The intent is documented and browsers always send `Origin` on a WebSocket handshake, so this is not directly exploitable from a browser. But it converts the origin check from a control into a suggestion for any non-browser caller, weakening defence-in-depth. Cookie auth is doing the real work.

**Recommendation:** Make the permissive branch opt-in (e.g. `VoiceLive:AllowMissingOrigin`, default `false`) so production deployments fail closed.

---

### S8 — CSP is weaker than documented; missing directives — **Medium**

**Where:** `web/src/VoiceLive.Web/Program.cs`; `README.md` line 156 describes it as "a strict `Content-Security-Policy`"

Current policy:

```
default-src 'self'; img-src 'self' data: blob:; media-src 'self' blob:;
connect-src 'self' wss: https:; script-src 'self'; style-src 'self' 'unsafe-inline';
worker-src 'self' blob:
```

Gaps:

| Missing/weak | Impact |
|---|---|
| `frame-ancestors 'none'` | `X-Frame-Options: DENY` covers current browsers, but CSP is the standard and X-Frame-Options is deprecated. |
| `base-uri 'none'` | An injected `<base>` tag can redirect all relative URLs. |
| `form-action 'self'` | Relevant given the login form and S4. |
| `object-src 'none'` | Legacy plugin vector. |
| `connect-src ... https: wss:` | Allows exfiltration to *any* host. The app only needs `'self'`. |
| `style-src 'unsafe-inline'` | Required today because `index.html` inlines all CSS; worth removing by extracting to a file. |

**Recommendation:** Tighten to `connect-src 'self'`, add the four missing directives, and extract inline CSS so `'unsafe-inline'` can be dropped. Also add `Cache-Control: no-store` on `/api/config` and authenticated HTML. Update the README to match reality.

---

### S9 — `AllowedHosts: "*"` — **Low**

**Where:** `web/src/VoiceLive.Web/appsettings.json`

No Host-header filtering. Low risk given App Service's own host binding and `httpsOnly`, but setting the deployed hostname closes host-header injection and cache-poisoning avenues cheaply. The Bicep template already knows the hostname (it sets `VoiceLive__AllowedOrigins__0`).

---

### S10 — Unescaped `innerHTML` sink in the client bootstrap — **Low**

**Where:** `web/frontend/src/main.ts`

```ts
document.body.innerHTML = `<pre style="color:red">Startup failed: ${...}</pre>`;
```

The interpolated value is an `Error.message` from local code, so this is not currently attacker-reachable — but it is the one raw-HTML sink in an otherwise disciplined codebase (everything else correctly uses `textContent`). It will be copied.

**Recommendation:** Build the element with `createElement` + `textContent`.

---

### S11 — CI workflow has no explicit `permissions` and no security scanning — **Low**

**Where:** `.github/workflows/ci.yml`

No `permissions:` block, so the workflow inherits repository-default `GITHUB_TOKEN` scope (write in many configurations) for jobs that only need to read code. There is also no CodeQL, dependency review, or `dotnet list package --vulnerable` gate.

**Recommendation:** Add `permissions: contents: read` at workflow level. Add `github/codeql-action` and `actions/dependency-review-action` on pull requests, and fail the build on vulnerable NuGet/npm packages. (Both ecosystems are clean today — this keeps them that way.)

---

## 4. Correctness findings

### C1 — `azure-custom` voice passes validation but fails every session — **Medium**

**Where:** `Config/WebConfig.cs` vs `Session/SessionOptionsBuilder.cs`

```csharp
// WebConfig.cs — accepted at startup
private static readonly string[] VoiceTypes = ["azure-realtime-native", "azure-standard", "azure-custom", "openai"];

// SessionOptionsBuilder.cs — throws at session start
"azure-custom" => throw new WebConfigValidationException("... 'azure-custom' is not supported yet ..."),
```

`docs/config-schema.md` (lines 26, 103) also documents it as supported. Net effect: an operator sets `voice.type: azure-custom`, startup validation passes, `/api/health` reports **Healthy**, and then *every* session fails at connect time with a config error surfaced as a session failure. This defeats the entire fail-fast design the config layer otherwise implements so carefully.

**Recommendation:** Remove `azure-custom` from `VoiceTypes` until it is implemented, and correct `docs/config-schema.md`. Add a test asserting the validated set and the buildable set are identical — this class of drift will recur otherwise.

---

### C2 — Documented-as-required `agent.json` keys are never read or validated — **Medium**

**Where:** `config/agent.json`, `docs/config-schema.md` lines 74–76 & 106–108, `Config/ServerSessionConfig.cs`

`config/agent.json` ships `agentVersion`, `conversationResumePolicy` and `groundingStrategy`. `docs/config-schema.md` marks the latter two **Required** and explicitly promises "Unknown values for … `agent.groundingStrategy`, or `agent.conversationResumePolicy` fail fast at startup."

`ServerAgentFile` declares only `AgentName`, `AgentProjectName`, `SafeQuestions`. A repository-wide search finds **zero** references to these three keys in any `.cs` or `.ts` file. They are silently ignored. An operator setting `groundingStrategy: rag` gets no error and no behaviour change — the worst kind of configuration failure.

**Recommendation:** Either implement and validate them, or remove them from `config/agent.json` and `docs/config-schema.md`. Consider rejecting unknown top-level keys in the config files so this cannot happen silently again.

---

### C3 — Config load crashes the app on I/O errors instead of reporting unhealthy — **Medium**

**Where:** `web/src/VoiceLive.Web/Program.cs`

```csharp
try { return new ConfigState(AppConfigLoader.Load(o.ConfigDir, o), null); }
catch (WebConfigValidationException ex) { return new ConfigState(null, ex.Message); }
```

`AppConfigLoader.Load` calls `File.Exists` and `File.ReadAllText` (for the grounding file, and inside `ReadServer<T>`/`ReadAvatarServer`). Those can throw `IOException`, `UnauthorizedAccessException`, `PathTooLongException` or `DirectoryNotFoundException` — none of which derive from `WebConfigValidationException`. Any of them escapes DI singleton construction and kills the app at startup.

That directly contradicts the design intent visible three lines later ("the app will report unhealthy until fixed") and the health-check plumbing built for exactly this case. A file-permission problem on the mounted `config/` directory turns a diagnosable unhealthy state into an opaque boot-loop.

**Recommendation:** Catch `Exception` (or at minimum add `IOException` and `UnauthorizedAccessException`) and funnel into `ConfigState(null, message)` so the existing health check reports it.

---

### C4 — `MaxConcurrentSessions` is unvalidated — **Medium**

**Where:** `Session/SessionGate.cs`, `Config/VoiceLiveOptions.cs`

`new SemaphoreSlim(max, max)` throws `ArgumentOutOfRangeException` for a negative value — an app-setting typo (`VoiceLive__MaxConcurrentSessions=-1`) crashes the app at startup with a stack trace rather than a config error. A value of `0` is worse: the app starts, reports **Healthy**, and silently refuses every session with "The server is at capacity."

**Recommendation:** Validate `MaxConcurrentSessions >= 1` in the config validation pipeline alongside the other checks, producing a normal `WebConfigValidationException`.

---

### C5 — Client has no reconnect backoff — **Low**

**Where:** `web/frontend/src/main.ts`

`README.md` advertises "automatic reconnect", but the implementation is a manual **Reconnect** button (`setReconnectHandler`). There is no automatic retry and no exponential backoff. If a user holds the button down during an outage, each click opens a fresh socket and — on the server — attempts a fresh Azure session.

**Recommendation:** Either implement automatic reconnect with exponential backoff and jitter plus a retry ceiling, or correct the README. Debounce the button while a connection attempt is in flight.

---

### C6 — `beforeunload` teardown cannot complete — **Low**

**Where:** `web/frontend/src/main.ts`

```ts
window.addEventListener("beforeunload", () => client.dispose());
```

`dispose()` starts an async cleanup that awaits `audioContext.close()`. The browser will not wait for it during unload. In practice the socket is torn down by the browser anyway and the server's `RequestAborted` fires, so the server side is safe — but the intent is not achieved. `pagehide` with synchronous `socket.close()` is the reliable idiom.

---

## 5. Performance findings

### P1 — AudioWorklet resamples with no anti-aliasing filter — **Medium**

**Where:** `web/src/VoiceLive.Web/wwwroot/pcm-worklet.js`

```js
const sourceIndex = Math.floor(this.positionNumerator / this.targetRate);
pcm[i] = this.sampleToInt16(this.getSample(sourceIndex));
```

This is nearest-neighbour (sample-and-hold) decimation with no low-pass pre-filter. Downsampling 48 kHz → 24 kHz without filtering folds everything above 12 kHz back into the audible band as aliasing distortion — audible as harshness or metallic artifacts, and it degrades upstream VAD and transcription accuracy.

The `sourceRate === targetRate` fast path means this only bites when the browser refuses the requested 24 kHz `AudioContext`. That is not an edge case: Firefox and Safari commonly clamp to the hardware rate (typically 48 kHz), so a significant share of real users hit the aliasing path.

**Recommendation:** Apply a simple low-pass (even a modest FIR or cascaded biquad at ~0.45 × target rate) before decimation, or use linear interpolation as a minimum improvement over sample-and-hold. Alternatively resample via `OfflineAudioContext`, which filters correctly.

### P2 — Worklet computes and posts PCM even while muted — **Medium**

**Where:** `pcm-worklet.js` + `main.ts`

The worklet converts and transfers every 128-sample block unconditionally; the *main thread* then discards it:

```ts
worklet.port.onmessage = (event) => {
  if (this.isCurrentSession(token) && this.streamingMic && this.socket?.readyState === WebSocket.OPEN)
    this.socket.send(event.data);
};
```

In `gated` mode — which is the **default** `activeMode` in `config/turntaking.json` — the mic is idle almost all the time, yet at 24 kHz the worklet still performs ~187 conversions/second, allocates an `Int16Array` each time, and pays a cross-thread `postMessage` + `ArrayBuffer` transfer for every one. All of it is thrown away. This is continuous wasted CPU on a real-time audio thread, exactly where jitter is least acceptable.

**Recommendation:** Push the gate into the worklet — `port.postMessage({ streaming: true/false })` from the main thread on turn start/end and mute toggle, and `return true` early in `process()` when not streaming. Cheap change, meaningfully lower idle CPU and battery use on kiosk hardware.

### P3 — Double allocation per inbound WebSocket frame on the audio hot path — **Medium**

**Where:** `Session/VoiceLiveWebSocketBridge.cs`, `PumpBrowserMessagesAsync`

```csharp
using var message = new MemoryStream();
...
message.Write(buffer, 0, result.Count);
...
var payload = message.ToArray();
```

The pooled 64 KB `ArrayPool` rental is good, but every frame then allocates a `MemoryStream` **and** a fresh `byte[]` via `ToArray()`. Audio frames arrive continuously (tens per second per session), so this is sustained Gen0 churn on the hottest path in the server, negating the pooling above it.

**Recommendation:** Fast-path the common single-read case (`result.EndOfMessage` on the first iteration) by passing `buffer.AsMemory(0, result.Count)` straight to `SendInputAudioAsync`, avoiding both allocations entirely. For the rare multi-frame case, use `MemoryStream.GetBuffer()` with the length rather than `ToArray()`, or a pooled `ArrayBufferWriter<byte>`.

### P4 — Frontend bundle is unminified and uncacheable — **Low**

**Where:** `web/frontend/package.json`, `wwwroot/index.html`

```json
"build": "esbuild src/main.ts --bundle --format=esm --outfile=../src/VoiceLive.Web/wwwroot/app.js"
```

No `--minify`, no `--target`. The production bundle ships full identifiers, comments and whitespace. Separately, `index.html` hardcodes `<script type="module" src="/app.js">` with no content hash, so there is no cache-busting: `UseStaticFiles` serves it with ETag/Last-Modified only, forcing a revalidation round-trip on every load and risking stale JS after a deploy if any intermediary caches it.

**Recommendation:** Add `--minify --target=es2022`. For cache-busting, either emit a hashed filename and inject it into `index.html` at build time, or append an asset version query string.

### P5 — B1 App Service plan, single instance, per-instance capacity gate — **Low**

**Where:** `infra/resources.bicep`

B1 is a burstable single-core tier hosting a workload that maintains persistent WebSockets and continuously marshals PCM16 audio. There is no autoscale rule and no `numberOfWorkers`. Combined with `MaxConcurrentSessions = 2`, that is a deliberate and reasonable demo footprint — but it is worth stating explicitly: `SessionGate` is a per-instance semaphore, so if the plan is ever scaled out the concurrency cap silently becomes *N × 2* rather than 2, which matters because the underlying constraint is Azure avatar-rendering quota, not app CPU.

**Recommendation:** Document the tier as demo-scale. If scale-out is ever intended, move the gate to a distributed counter or keep `numberOfWorkers: 1` pinned.

### P6 — `SemaphoreSlim` never disposed — **Low**

**Where:** `Session/VoiceLiveWebSocketBridge.cs`

`_sendLock` is created per bridge, and the bridge is created per session by `VoiceLiveBridgeFactory` — but `VoiceLiveWebSocketBridge` is not `IDisposable` and `_sendLock` is never disposed. `SemaphoreSlim` only allocates a kernel handle if `AvailableWaitHandle` is touched (it isn't here), so there is no handle leak in practice — but it is a latent one if the class evolves.

**Recommendation:** Implement `IDisposable`/`IAsyncDisposable` and dispose in `Program.cs` around `RunAsync`.

### P7 — `getSample()` is a linear scan per output sample — **Low**

**Where:** `pcm-worklet.js`

```js
getSample(index) { let offset = index; for (const chunk of this.pending) { ... } }
```

`compact()` keeps `pending` at one or two chunks in steady state, so real-world cost is fine. But the structure is O(chunks × outputSamples), and any future change that lets the queue grow (a stall, a larger render quantum, a backpressure pause) degrades quadratically on the audio thread. Worth flattening into a single ring buffer with an absolute read index.

---

## 6. Best-practice and maintainability findings

### B1 — Two overlapping authorization mechanisms — **Medium**

**Where:** `web/src/VoiceLive.Web/Program.cs`

A hand-rolled middleware performs the real gating:

```csharp
var anon = path.StartsWithSegments("/login") || path.StartsWithSegments("/logout")
        || path.Equals("/api/health", StringComparison.OrdinalIgnoreCase);
if (!anon && !(ctx.User.Identity?.IsAuthenticated ?? false)) { ... }
```

…while endpoints *also* carry `.AllowAnonymous()`, and `app.UseAuthorization()` runs afterwards. The `.AllowAnonymous()` calls are decorative — the custom middleware already let those paths through by string matching. This is a genuine footgun: a future endpoint added with `.RequireAuthorization()` gets gated by a path-prefix string list that knows nothing about it, and a new anonymous endpoint must remember to update a hardcoded list in a completely different file.

Note also that the path list uses `StartsWithSegments("/logout")`, so `/logout/anything` is anonymous, and the ordering means `UseStaticFiles` is correctly placed *after* the gate (good — that part is right and easy to get wrong).

**Recommendation:** Replace with the framework mechanism: `AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())`, keep `.AllowAnonymous()` on the three public endpoints, and use `StatusCodes`/`OnRedirectToLogin` on the cookie options to return 401 for `/api` and `/ws` instead of redirecting. One mechanism, declarative, colocated with each endpoint.

### B2 — Test-only public API duplicating the real parser — **Medium**

**Where:** `Session/VoiceLiveWebSocketBridge.cs`, `web/tests/.../ControlMessageTests.cs`

```csharp
public static bool TryGetControlType(string json, out string? type)
```

Nothing in production calls this. It exists for `ControlMessageTests`, and it *reimplements* the parse that `HandleControlMessageAsync` performs inline. The tests therefore validate a parallel copy of the logic, and the real dispatcher — including every `case` in the switch, the `say` handling, and the turn-id lifecycle — has **no** direct coverage. A regression in `HandleControlMessageAsync` leaves all 90 tests green.

**Recommendation:** Extract control-frame parsing and dispatch into a genuinely testable seam (e.g. an `IVoiceLiveSession` abstraction or a pure `ControlFrame.TryParse` returning a discriminated result that `HandleControlMessageAsync` then consumes). Delete `TryGetControlType`.

### B3 — Coverage gaps in security-relevant paths — **Medium**

**Where:** `web/tests/VoiceLive.Web.Tests/`

The 90 passing tests are genuinely good — `ServerSessionConfigTests` in particular is thorough. But a grep across the suite finds no coverage for:

- `OriginAllowed` — the WebSocket origin check (S7)
- the `MaxConcurrentSessions` capacity gate end-to-end through `/ws/session` (S6)
- the `say` control frame (S5)
- `/ws/session` behaviour at all, beyond `TryGetControlType`

These are precisely the paths where the security findings above live. `SessionGateTests` covers the semaphore in isolation, not its integration.

**Recommendation:** Add integration tests using `WebApplicationFactory`'s WebSocket client for origin rejection (403), capacity rejection (the `t:"error"` startup frame), and unauthenticated rejection (401).

### B4 — Fragile rate-limit test — **Low**

**Where:** `web/tests/VoiceLive.Web.Tests/AuthTests.cs`

`Login_rejects_sixth_attempt_from_same_ip_with_429` passes only because `WebApplicationFactory` leaves `Connection.RemoteIpAddress` null, so every request lands in the `"unknown"` partition. The test name asserts "from same IP" but no IP is involved. It would not catch the S1 bypass, and it will break if the harness ever populates a remote IP.

**Recommendation:** Set `RemoteIpAddress` explicitly via middleware in the test host, and add a companion test proving that *different* IPs get *independent* buckets — which is the behaviour S1 shows is currently unenforceable.

### B5 — Inconsistent namespace qualification in `Program.cs` — **Low**

`Program.cs` opens with `using VoiceLive.Web.Config;` and `using VoiceLive.Web.Session;`, then writes fully-qualified names anyway:

```csharp
builder.Services.AddSingleton<VoiceLive.Web.Config.ConfigState>(sp => { ... });
```

…while the very next block uses bare `SessionGate` and `IVoiceLiveBridgeFactory`. Purely cosmetic, but it makes an otherwise clean 187-line file noisier than it needs to be.

### B6 — Brittle string-matching for avatar capacity errors — **Low**

**Where:** `Session/VoiceLiveWebSocketBridge.cs`

```csharp
signal.Contains("avatar", ...) && (signal.Contains("exhausted", ...) || signal.Contains("capacity", ...))
```

The voice-only fallback — a genuinely nice reliability feature — hinges on substring matching against service error codes. A wording change upstream silently converts a graceful degradation into a hard session failure.

**Recommendation:** Match on the exact known error codes with the substring heuristic as a documented fallback, and log at `Warning` when the heuristic (rather than an exact match) fires, so drift is visible in telemetry.

### B7 — Single hardcoded API version — **Low**

**Where:** `Session/VoiceLiveServiceVersionMapper.cs`

`Map()` accepts exactly `"2025-10-01"`. Every Voice Live service version bump requires a code change, rebuild and redeploy — even though `VoiceLive__ApiVersion` is exposed as a runtime app setting, implying it is tunable. That is a misleading configuration surface.

**Recommendation:** Drive the mapping from `Enum.TryParse` over `VoiceLiveClientOptions.ServiceVersion`, or document clearly that the setting is validated against a compile-time allow-list.

### B8 — `ReadAvatarServer` is disproportionately complex — **Low**

**Where:** `Config/ServerSessionConfig.cs`

The method enumerates the root JSON object **four separate times** with case-insensitive name comparison to check `customized`, `preview`, `style` exclusivity and `video.background` shape, then deserializes and clones. It is markedly harder to follow than the sibling `ReadServer<T>` path, and the `break` inside the first `foreach` means only the *first* matching property is examined in several loops.

**Recommendation:** Enumerate once into a case-insensitive dictionary of `JsonElement`, then run the checks against it.

### B9 — No repo-wide build hygiene — **Low**

Missing across the solution:

- No `Directory.Build.props` — so no `TreatWarningsAsErrors`, no `EnableNETAnalyzers`, no `AnalysisLevel`, no centrally pinned `LangVersion`.
- No `.editorconfig` — no enforced style, and no way to make the analyzer rules in B5's category actionable.
- No `global.json` — CI pins `10.0.x` (floating), so an SDK feature-band bump can change behaviour without a repo change.
- CI never runs `dotnet publish`, so the `BuildFrontend` MSBuild target (which runs `npm ci` + `npm run build` on `Publish`) is not exercised — the one path that actually produces the deployed artifact. `dotnet test` is invoked with `-p:SkipFrontendBuild=true`, and the frontend job builds the bundle separately, so the *combined* path is untested.
- No `dotnet format --verify-no-changes` step.

### B10 — Undeclared Python dependency in the JS test suite — **Low**

**Where:** `web/frontend/playwright.config.ts`

```ts
command: "python3 -m http.server 4173 --bind 127.0.0.1 --directory ../src/VoiceLive.Web/wwwroot"
```

A Node test suite that silently requires Python 3 on `PATH`. It works on the `ubuntu-latest` runner and on most dev machines, but it is an invisible prerequisite and will fail confusingly on a clean Windows or minimal-container environment.

**Recommendation:** Use a Node static server (`npx serve`, or a tiny `http` script) so the toolchain is self-contained.

### B11 — License file naming — **Low**

`licence.md` will not be detected as a license by GitHub's license API, `dotnet pack`, or most SBOM/compliance tooling, all of which look for `LICENSE`/`LICENSE.md`/`LICENCE`. Rename to `LICENSE.md` (or `LICENSE`) and reference it from the README.

### B12 — Config is read once at startup with no reload path — **Low**

`ConfigState` is a DI singleton constructed at startup. Editing anything under `config/` requires a full app restart, and there is no `IOptionsMonitor`/`FileSystemWatcher` path. This is a defensible choice for a kiosk (it guarantees a session can never observe a half-applied config), but it is not stated anywhere in `docs/runbook.md`, where an operator would look for it.

---

## 7. What the codebase does well

Worth recording, because these are deliberate choices that many comparable projects get wrong:

- **Credential isolation is correct.** No Azure key, token or endpoint secret ever reaches the browser. `DefaultAzureCredential` with managed identity in Azure and `disableLocalAuth: true` on the Cognitive Services account means there is no API key to leak. `/api/config` exposes a deliberately curated `ClientConfig` projection rather than the server config object.
- **Cancellation and teardown are genuinely well done.** `CreateLinkedTokenSource(requestAborted)`, `Task.WhenAny` followed by `cts.Cancel()` and `WhenAll(SwallowCancellation(...))`, the `_sendLock` serializing concurrent sends from both pumps, and `CloseIfOpenAsync` guarding socket state — this is careful, correct async code.
- **The client-side session-token pattern is excellent.** `isCurrentSession(token, socket)` guards every async continuation in `main.ts`, so a reconnect during in-flight WebRTC negotiation or `getUserMedia` cannot corrupt the new session. Combined with the idempotent `disconnectPromise` teardown, this eliminates an entire class of race conditions that plague WebRTC clients.
- **Error messages are sanitized before reaching the client.** `SafeError()` maps exceptions to fixed strings and never leaks stack traces or internal detail — while still logging the full exception server-side.
- **Configuration validation is thorough and aggregating.** Errors accumulate into a single actionable list rather than failing on the first problem, messages consistently use `file: field: problem` form, and the invalid-config-means-unhealthy-not-crashed design is the right call (C3 notwithstanding).
- **XSS discipline in the view layer.** `views.ts` builds every element with `createElement` and `textContent` throughout — including for server-supplied `safeQuestions` and transcripts. S10 is the sole exception in ~440 lines.
- **The avatar-capacity voice-only fallback** is a thoughtful reliability feature with a genuinely helpful operator-facing message.
- **Observability is properly wired**: OpenTelemetry meters for active sessions, session duration and errors, tagged error counters, `BeginScope` with a session id, health checks, and Azure Monitor wired conditionally on the connection string being present.
- **Infrastructure follows least privilege**: system-assigned identity, two narrowly scoped role assignments, `httpsOnly`, `minTlsVersion: 1.2`, `ftpsState: Disabled`, and `healthCheckPath` wired to the real endpoint.
- **`scripts/setup-agent.sh` is defensively written** — `set -euo pipefail`, GET-only by explicit design and documented as such, degrades gracefully when `jq` or env vars are missing, and always exits 0.
- **Documentation is unusually complete** for a project this size — architecture diagrams, a config schema reference, a runbook, a rehearsal checklist, and preserved design/plan history.

---

## 8. Prioritized recommendations

**Before any exposed deployment**

1. **S2** — Remove committed credentials; move to user secrets; rotate the `testlab-f` endpoint if real.
2. **S1** — Configure `ForwardedHeadersOptions` explicitly with known proxies; add a global `/login` backstop limiter.
3. **S5** — Constrain `say` to a server-side allow-list or an index; add a length cap and per-connection rate limit.
4. **S6** — Add absolute + idle session timeouts to `/ws/session`.
5. **S3** — Move `Auth__Password` to a Key Vault reference.
6. **S4** — Add antiforgery to `POST /login`.

**Next**

7. **C1**, **C2** — Reconcile config validation, runtime capability and `docs/config-schema.md`; add a drift test.
8. **C3**, **C4** — Make config-load failures and bad `MaxConcurrentSessions` produce unhealthy-with-message rather than a crash.
9. **B1** — Replace the hand-rolled auth middleware with a `FallbackPolicy`.
10. **B2**, **B3** — Make the real control-frame dispatcher testable; cover origin, capacity and `say`.
11. **S8** — Tighten the CSP and align the README claim.

**Then**

12. **P1**, **P2** — Fix worklet aliasing and gate PCM production while muted.
13. **P3** — Remove per-frame allocations on the server audio path.
14. **P4** — Minify and cache-bust the bundle.
15. **B9**, **S11** — Add `Directory.Build.props`, `.editorconfig`, `global.json`, a `dotnet publish` CI job, workflow `permissions`, and dependency/code scanning.
16. **C5**, **B5**–**B12** — Documentation accuracy, naming, and cleanup.

---

## 9. Scope and method

Reviewed by direct reading of all 84 tracked files (build artifacts under `bin/`/`obj/` excluded), plus dynamic verification: the .NET test suite was executed (90/90 pass), and NuGet and npm dependency trees were audited for known vulnerabilities and staleness. Findings involving runtime behaviour under load, real Azure Voice Live service responses, or browser-specific `AudioContext` sample-rate negotiation were reasoned about from the code and are flagged as such rather than empirically confirmed.
