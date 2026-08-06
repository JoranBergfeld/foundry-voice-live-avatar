# Merged Implementation Review — `foundry-voice-live-avatar`

**Composite of:** [`opus-review.md`](opus-review.md) (Claude Opus 5) and [`sol-review.md`](sol-review.md) (GPT‑5.6 Sol)
**Merged:** 2026-08-05
**Commit reviewed:** `d5110dc` (`docs: add MIT License file`)
**Scope:** ASP.NET Core 10 server, TypeScript browser client, AudioWorklet, Bicep infrastructure, CI, configuration, documentation.

This document consolidates two independent reviews of the same commit. Findings are grouped by **agreement level**, because agreement is itself signal: a finding both reviewers reached independently is high-confidence, while a single-source finding may reflect either deeper analysis or a false positive.

Part A merges the two reviews. Part B is a **new** documentation-alignment review performed during this merge pass.

---

## A1. Executive summary

Both reviewers independently reached the same overall verdict: **a well-engineered codebase with a clean dependency posture and a green test suite, whose weaknesses are design-level gaps rather than rot.** Both praised the same architectural decisions — server-side credential custody, WebRTC media bypass, rigorous config validation, and disciplined async teardown.

Both also independently verified the same baseline:

| Check | Opus | Sol |
|---|---|---|
| .NET test suite | 90/90 passing | 90/90 passing |
| NuGet vulnerability audit (incl. transitive) | Clean | Clean |
| npm audit (frontend) | 0 vulnerabilities | Clean (prod + full) |
| Playwright Chromium | — | 23/23 passing |
| Frontend TypeScript check | — | Passing |
| Outdated packages | 1 minor, 1 patch | — |
| Repository hygiene | — | Clean; no tracked artifacts or secret-like filenames |

**Where they diverge:** Opus rated 2 Critical and 4 High; Sol found no Critical or High. The gap is not disagreement about facts — it is a different threat model. Sol assessed the app broadly as configured and deployed; Opus additionally modelled a **motivated authenticated or network-adjacent attacker** and traced two exploit chains (rate-limiter bypass, `say` prompt injection) that Sol did not examine. Sol, conversely, found a **live functional defect in the primary unattended view** that Opus missed entirely.

Neither review is a superset of the other. The union is the actionable set.

**Consolidated verdict:** fit for purpose as a controlled, staffed, on-stage kiosk behind a shared password. **Not ready for untrusted or internet-facing users** until the Critical and High findings below are closed.

---

## A2. Severity model

Opus's four-level scale is adopted, since Sol's two levels map onto it cleanly. Where the reviewers assigned different severities to the same defect, **the higher is used** and both are shown.

| Level | Meaning |
|---|---|
| **Critical** | Exploitable or service-affecting; fix before any exposed deployment. |
| **High** | Material security, cost or correctness risk; fix soon. |
| **Medium** | Real defect or notable gap; schedule it. |
| **Low** | Polish, consistency, maintainability. |

---

## A3. Consensus findings — both reviewers, independently

These are the highest-confidence findings in the review. Two reviewers with different threat models converged on all seven.

### M-01 — No idle or absolute session timeout; capacity gate trivially exhausted — **High**
> Opus **S6** (High) · Sol **MED-04** (Medium) — *both ranked this in their top four.*

**Where:** `Program.cs`, `Session/SessionGate.cs`, `Session/VoiceLiveWebSocketBridge.cs`, `Config/VoiceLiveOptions.cs`, `appsettings.json`

`MaxConcurrentSessions` defaults to `2`. `SessionGate.TryEnter()` reserves a slot for the entire WebSocket lifetime, and nothing bounds that lifetime: no absolute cap, no idle timeout, no cap on cumulative audio bytes or control frames. The client pings every 25 s and `KeepAliveInterval` is 30 s, so a merely-open socket stays healthy forever.

Two browser tabs left open — accidentally or deliberately — permanently deny service to everyone **and** hold two billed Azure Voice Live sessions with avatar rendering open indefinitely. This is simultaneously a DoS path and a runaway-cost path. The 1 MiB message cap bounds individual messages but does nothing about idle capacity exhaustion. The gate is also per-instance, so the cap silently becomes *N × 2* if the plan is ever scaled out.

Existing tests cover gate accounting in isolation (`SessionGateTests`), not long-lived or inactive sessions, and not the gate end-to-end through `/ws/session`.

**Recommendation:** Add configurable absolute (e.g. 30–60 min) and idle timeouts, linked into the existing `CancellationTokenSource`, which already propagates cleanly to both pumps. Key the idle timer on **genuine user activity** — audio frames or turn events — deliberately *not* `ping`, which a zombie tab keeps sending. Record the termination reason, emit a metric on gate rejection so exhaustion is observable, alert on sustained saturation, and test idle-slot reclamation.

---

### M-02 — `Auth__Password` stored as a plaintext App Service setting — **High**
> Opus **S3** (High) · Sol **MED-02** (Medium)

**Where:** `infra/main.bicep`, `infra/resources.bicep`

```bicep
{ name: 'Auth__Password', value: authPassword }
```

The Bicep parameter is correctly marked `@secure()`, which protects deployment *inputs* — but the value then lands as an ordinary application setting. It is readable by any principal holding `Microsoft.Web/sites/config/list/action` (which includes Contributor and Website Contributor), visible in the portal, and returned by `az webapp config appsettings list`. `@secure()` provides a false sense of protection here. The sole operator credential is neither independently managed nor rotated.

**Recommendation:** Provision a Key Vault, store the password as a secret, grant the site's system-assigned identity **only** `Key Vault Secrets User`, and set the app setting to a Key Vault reference (`@Microsoft.KeyVault(SecretUri=...)`). The site already has a managed identity and RBAC assignments, so this is a small increment. Document rotation, and avoid granting broad access to application configuration.

---

### M-03 — CI has no security, dependency, or infrastructure gates — **Medium**
> Opus **S11** (Low) · Sol **MED-03** (Medium) — *Sol rated this higher; Sol's rating is used.*

**Where:** `.github/workflows/ci.yml`

CI runs unit tests, TypeScript checking, the frontend build, and Chromium Playwright tests. It does not:

- declare least-privilege `permissions:` — so jobs that only read code inherit the repository-default `GITHUB_TOKEN` scope, which is write in many configurations;
- pin third-party actions to immutable commit SHAs;
- run NuGet or npm vulnerability checks;
- run CodeQL or any static analysis;
- validate Bicep compilation;
- scan repository content for committed secrets.

Both reviewers audited dependencies by hand and found them clean — but nothing enforces that on future changes.

**Recommendation:** Add `permissions: contents: read`, pin actions to commit SHAs, configure Dependabot, add `actions/dependency-review-action` and secret scanning, enable CodeQL, add an `az bicep build` job, and fail the build on vulnerable NuGet/npm packages. Extend browser coverage beyond Chromium where display reliability depends on it (see M-05).

---

### M-04 — AudioWorklet resamples with no anti-aliasing filter — **Medium**
> Opus **P1** (Medium) · Sol **LOW-04** (Low)

**Where:** `web/src/VoiceLive.Web/wwwroot/pcm-worklet.js`

```js
const sourceIndex = Math.floor(this.positionNumerator / this.targetRate);
pcm[i] = this.sampleToInt16(this.getSample(sourceIndex));
```

Nearest-neighbour (sample-and-hold) decimation with no low-pass pre-filter. Downsampling 48 kHz → 24 kHz without filtering folds everything above 12 kHz back into the audible band as aliasing distortion — audible as harshness, and it degrades upstream VAD and transcription accuracy, which matters directly for the stated noisy-stage use case.

The `sourceRate === targetRate` fast path means this only bites when the browser refuses the requested 24 kHz `AudioContext` — but Firefox and Safari commonly clamp to the hardware rate (typically 48 kHz), so this is a mainstream path, not an edge case. `pcm-worklet.spec.ts` verifies the current sample-selection algorithm rather than audio fidelity, so it would not detect the problem.

**Recommendation:** Apply a low-pass filter (a modest FIR or cascaded biquad at ~0.45 × target rate) before decimation; linear interpolation is a minimum improvement over sample-and-hold, and `OfflineAudioContext` filters correctly. Add signal-based tests measuring attenuation above 12 kHz while preserving the speech band.

---

### M-05 — Tests validate the wrong thing; failure paths are hidden by the harness — **Medium**
> Opus **B3**, **B4** · Sol **MED-01**, **MED-04**, **LOW-04** (as supporting evidence)

Both reviewers independently observed that the 90 green tests create false confidence in exactly the areas where the findings live:

| Path | How the harness hides it |
|---|---|
| Avatar autoplay | `browser-mocks.ts` always resolves `play()`, so the rejection path (H-05) is never exercised. |
| Login rate limiter | `AuthTests` passes only because `WebApplicationFactory` leaves `RemoteIpAddress` null, so all requests share the `"unknown"` partition. The test asserts "from same IP" while no IP is involved — it cannot catch C-01. |
| Capacity gate | `SessionGateTests` covers the semaphore in isolation, never end-to-end through `/ws/session`, and never with a long-lived or idle session. |
| PCM resampling | Tests assert the current algorithm, not audio fidelity — so M-04 is invisible. |
| WebSocket origin check | No coverage at all. |
| `say` control frame | No coverage at all. |
| `HandleControlMessageAsync` | No direct coverage; tests exercise a **parallel reimplementation** (see L-08). |

**Recommendation:** Add `WebApplicationFactory` WebSocket integration tests for origin rejection (403), capacity rejection (`t:"error"` frame), and unauthenticated rejection (401). Set `RemoteIpAddress` explicitly in the test host and add a companion test proving different IPs get independent buckets. Add an E2E test where `play()` initially rejects.

---

### M-06 — Weak validation of control and server frames — **Medium**
> Opus **B2** (parser duplication) · Sol **LOW-01** (server-side), **LOW-03** (client-side)

Two sides of the same gap: the wire protocol is trusted beyond its discriminator.

**Server side** (`VoiceLiveWebSocketBridge.cs`): malformed JSON is ignored safely, but a *well-formed* message with wrong value types — `{"t":"say","text":123}` or `{"t":"avatar-offer","sdp":123}` — calls `JsonElement.GetString()` on a non-string, throwing `InvalidOperationException`, which reaches the bridge-wide handler and closes the caller's session.

**Client side** (`main.ts`, `views.ts`): `parseServerFrame` validates only the `t` discriminator and casts the rest. A `ready` frame with an invalid `config` or `safeQuestions` can throw inside `onReady`/`setConfig`; the rejected handler is **not** routed through `disconnect()`, so the user gets neither an error nor a reconnect control — a silent hang. The backend currently emits valid frames, so this is resilience against future protocol drift.

**Recommendation:** Check `ValueKind == JsonValueKind.String` before reading `t`, `text`, and `sdp`; reject or ignore invalid control frames consistently. Validate required fields on each server frame before dispatch (or use a small schema validator) and route failures through the normal disconnect/error flow. Add protocol-level tests for wrong field types and malformed frames on both sides.

---

### M-07 — `AllowedHosts: "*"` — **Low**
> Opus **S9** (Low) · Sol **LOW-07** (Low)

**Where:** `web/src/VoiceLive.Web/appsettings.json`

No Host-header filtering, removing an ASP.NET Core defence-in-depth layer against malformed Host headers and host-based cache-poisoning or link-generation issues. Low risk given App Service host binding and `httpsOnly`. The Bicep template already knows the hostname — it sets `VoiceLive__AllowedOrigins__0` from it.

**Recommendation:** Set production `AllowedHosts` to the deployed App Service hostname plus any supported custom domains.

---

## A4. Critical and High findings — Opus only

Sol did not examine these paths. All were verified against the code during this merge and are confirmed accurate.

### C-01 — Login rate limiter bypassable via spoofed `X-Forwarded-For` — **Critical**
> Opus **S1**

**Where:** `Program.cs` (rate limiter), `infra/resources.bicep` (`ASPNETCORE_FORWARDEDHEADERS_ENABLED`)

The limiter partitions on `context.Connection.RemoteIpAddress`. Bicep sets `ASPNETCORE_FORWARDEDHEADERS_ENABLED = 'true'`, which enables `ForwardedHeaders` middleware **without** restricting `KnownProxies`/`KnownNetworks` — so ASP.NET Core rewrites `RemoteIpAddress` from whatever `X-Forwarded-For` the caller supplies. Rotating that header yields a fresh 5-per-minute bucket per value: effectively unlimited attempts.

This compounds with C-02: one credential pair, human-chosen, no lockout, no MFA, no delay. The 5/min limiter that `README.md` documents as a security control is the **only** brute-force defence, and it does not hold.

The fallback partition key `"unknown"` inverts the problem on any non-App-Service host (Container Apps, Docker, self-hosted, the test harness): **every** client collapses into one bucket, permitting 5 total logins per minute globally. The limiter is therefore either bypassable or globally throttling depending on host — never correct.

**Recommendation:** Configure `ForwardedHeadersOptions` explicitly in code with `KnownNetworks`/`KnownProxies` set to the actual front end rather than using the env-var shortcut. Partition on the *validated* remote IP. Add a coarser global `/login` limiter as a backstop, plus exponential backoff or lockout after repeated failures.

---

### C-02 — Working credentials committed to the repository — **Critical**
> Opus **S2**

**Where:** `web/src/VoiceLive.Web/appsettings.Development.json`

```json
"Auth": { "Username": "operator", "Password": "rehearsal" },
"VoiceLive": { "Endpoint": "https://testlab-f.services.ai.azure.com", "Mode": "agent" }
```

A working username/password pair is in version control **and** published in four separate documents (see D-04). Because `appsettings.Development.json` layers over `appsettings.json`, anyone who deploys with `ASPNETCORE_ENVIRONMENT=Development`, or who forgets to override `Auth__*`, ships a publicly known credential — a genuine "works by accident in production" hazard.

Separately, `https://testlab-f.services.ai.azure.com` looks like a real tenant-specific Azure AI Services hostname: information disclosure about internal infrastructure, and it will silently mis-target a developer's local session.

**Recommendation:** Move both to `dotnet user-secrets` (add `UserSecretsId` to the `.csproj`). Leave `appsettings.Development.json` with non-sensitive logging overrides only. **Rotate the `testlab-f` resource if it is real.** Replace the published defaults in all four documents with instructions to set user secrets.

---

### H-01 — `say` control frame is an unrestricted prompt-injection and cost channel — **High**
> Opus **S5**

**Where:** `Session/VoiceLiveWebSocketBridge.cs`, `HandleControlMessageAsync`

The UI only ever sends server-supplied `safeQuestions` and a fixed repeat string — but the server accepts **any** text from **any** holder of a session cookie (a browser console, `websocat`, a compromised kiosk tab).

- **Model mode:** `StartResponseAsync(prompt)` supplies *per-response instructions*, so a client can override the curated grounding prompt from `config/grounding/company-direction.md` and make the on-stage avatar say arbitrary things. For a public presentation avatar this is a concrete reputational risk.
- **Agent mode:** the text is injected as a user turn — same outcome, one layer removed.
- **No length cap.** `MaxMessageBytes` bounds the frame at 1 MiB, so a ~1 MiB instruction blob is forwarded to Azure verbatim.
- **No rate limit.** The login limiter does not apply to WebSocket frames; a loop of `say` frames drives unbounded inference and avatar-rendering spend.

**Recommendation:** Treat `say` as privileged. Either validate the text against the server's own `config.Agent.SafeQuestions` allow-list plus the fixed repeat prompt, or replace the free-text field with an **index** into `safeQuestions` so no client-authored text reaches the model. Add a hard length cap and a per-connection token bucket in both cases. If free text must remain for operator use, gate it behind a distinct role or claim rather than plain authentication.

---

### H-02 — No CSRF/antiforgery protection on `POST /login` and `POST /logout` — **High**
> Opus **S4**

**Where:** `Auth/LoginEndpoints.cs`

Both endpoints read the form via `ctx.Request.ReadFormAsync()`. Minimal APIs auto-apply antiforgery validation only when a handler binds `[FromForm]`, so no token is issued or checked.

`SameSite=Lax` blocks the auth cookie on cross-site POSTs, substantially mitigating *logout* CSRF. It does **not** mitigate **login CSRF**, which requires no existing cookie: a third-party page can silently POST attacker-controlled credentials and authenticate the victim's browser into the attacker's session — a vector for feeding poisoned transcripts and tool activity into a session the attacker controls, or for session-fixation confusion on a shared kiosk.

**Recommendation:** `AddAntiforgery()`, emit a token in the login form, and validate on POST — or bind the form model with `[FromForm]` so the framework validates automatically. Add `form-action 'self'` to the CSP.

---

### H-03 — `azure-custom` voice passes validation but fails every session — **Medium→High**
> Opus **C1** (Medium) — *raised to High here: it defeats the fail-fast design and misreports health.*

**Where:** `Config/WebConfig.cs` vs `Session/SessionOptionsBuilder.cs`, and `docs/config-schema.md`

```csharp
// WebConfig.cs:22 — accepted at startup
private static readonly string[] VoiceTypes = ["azure-realtime-native", "azure-standard", "azure-custom", "openai"];

// SessionOptionsBuilder.cs:55 — throws at session start
"azure-custom" => throw new WebConfigValidationException("... 'azure-custom' is not supported yet ...")
```

An operator sets `voice.type: azure-custom`, startup validation passes, `/api/health` reports **Healthy**, and then *every* session fails at connect time. `docs/config-schema.md` documents it as a supported value in two places. For a rehearsal-driven kiosk this is the worst possible failure shape: green health, dead sessions, discovered on stage.

**Recommendation:** Remove `azure-custom` from `VoiceTypes` until implemented, and correct `docs/config-schema.md`. Add a test asserting the validated set and the buildable set are identical — this class of drift will otherwise recur.

---

### H-04 — Documented-as-required `agent.json` keys are never read — **Medium→High**
> Opus **C2** (Medium) — *raised to High: confirmed zero code references, while docs promise fail-fast validation.*

**Where:** `config/agent.json`, `docs/config-schema.md`, `Config/ServerSessionConfig.cs`

`config/agent.json` ships `agentVersion`, `conversationResumePolicy` and `groundingStrategy`. `docs/config-schema.md` marks the latter two **Required** and explicitly promises: *"Unknown values for … `agent.groundingStrategy`, or `agent.conversationResumePolicy` fail fast at startup."*

`ServerAgentFile` declares only `AgentName`, `AgentProjectName`, `SafeQuestions`. **A repository-wide search over all `.cs` and `.ts` files returns zero references to any of the three keys** — confirmed during this merge. An operator setting `groundingStrategy: rag` gets no error, no validation, and no behaviour change: the worst kind of configuration failure, and it directly contradicts a written promise.

**Recommendation:** Either implement and validate them, or remove them from `config/agent.json` **and** `docs/config-schema.md`. Consider rejecting unknown top-level config keys so this cannot recur silently.

---

## A5. Medium and High findings — Sol only

### H-05 — Avatar autoplay failure destroys the session in unattended views — **Medium→High**
> Sol **MED-01** (Medium) — *raised to High: it breaks the primary display surface, and Opus missed it entirely.*

**Where:** `main.ts:259-266`, `views.ts:107-111`, `views.ts:275-281`, `views.ts:404-408`, `tests/browser-mocks.ts:334-340`

The avatar media stream carries audio, and the app calls `video.play()` programmatically while the element is **not muted**. Browsers routinely reject media-with-audio autoplay until a user gesture. Verified in code:

```ts
this.view.avatar.play().catch((error: unknown) => {
  if (error instanceof DOMException && error.name === "AbortError") return;
  void this.disconnect(`Browser blocked avatar playback: ...`, token);
});
```

Any non-`AbortError` rejection — including `NotAllowedError`, the *expected* autoplay error — tears down the **entire Voice Live session**, not just video. This is most harmful in `?view=display`, documented as an unattended secondary-screen surface that by design offers no initial interaction and no microphone. `browser-mocks.ts` always resolves `play()`, so the path is untested (see M-05).

**Recommendation:** Treat `NotAllowedError` as a recoverable media condition, not a session error. Retry with the avatar **muted** so video starts, then expose a user-gesture control to enable audio. Never tear down the Voice Live session solely because autoplay was blocked. Add an E2E test where `play()` initially rejects.

---

### L-01 — Upstream service error details forwarded to the browser — **Low**
> Sol **LOW-02**

**Where:** `Session/VoiceLiveWebSocketBridge.cs:179-198`

Non-capacity `SessionUpdateError` messages include the upstream service's raw error message in the client-facing response. Every other exception path uses the sanitized `SafeError()` helper — which Opus specifically praised as a strength — making this path an inconsistent exception that can disclose service internals to authenticated users.

**Recommendation:** Return a stable generic message plus a documented error code; log the full upstream message server-side only, with the session correlation id.

---

### L-02 — Login comparison timing varies with credential length — **Low**
> Sol **LOW-05**

**Where:** `Auth/LoginEndpoints.cs:40-50`

`CryptographicOperations.FixedTimeEquals` is constant-time only for equal-length buffers and returns early when lengths differ, theoretically leaking the configured username and password lengths. The IP limiter reduces practical exploitability — but note C-01 shows that limiter is bypassable, which strengthens this finding beyond Sol's original assessment.

**Recommendation:** Hash both supplied and configured values to a fixed-length digest before comparing with `FixedTimeEquals`.

---

### L-03 — Azure AI Services account permits public network access — **Low**
> Sol **LOW-06**

**Where:** `infra/resources.bicep:12-27`

`publicNetworkAccess: 'Enabled'` with no network ACLs or private endpoint. `disableLocalAuth: true` plus managed-identity RBAC substantially reduces risk, but the endpoint remains reachable from arbitrary networks.

**Recommendation:** For stricter environments use a private endpoint with App Service VNet integration, or restrict with network ACLs. Retain `disableLocalAuth: true`.

---

### L-04 — Platform diagnostic logs not connected to Log Analytics — **Low**
> Sol **LOW-08**

**Where:** `infra/resources.bicep:34-48` — no `Microsoft.Insights/diagnosticSettings` resources

Application Insights covers application telemetry, but App Service platform logs and AI Services audit/resource logs are not streamed to the Log Analytics workspace, limiting incident investigation.

**Recommendation:** Add diagnostic settings for the Web App and the AI Services account covering HTTP, console, authentication, audit and resource log categories, with an appropriate retention policy.

---

### L-05 — Dynamic connection status not announced to assistive technology — **Low**
> Sol **LOW-09**

**Where:** `views.ts:64-69`, `views.ts:139-146`

Connection, WebRTC, microphone, turn, speech and avatar status changes update plain `<p>` elements. Screen-reader users are not notified of these transitions, although error and non-fatal banners already use appropriate roles.

**Recommendation:** Mark the status container `role="status"` with `aria-live="polite"`, and add an accessibility assertion to the browser tests.

---

## A6. Remaining findings — Opus only

Condensed; see `opus-review.md` for full evidence.

### Correctness

| ID | Finding | Sev |
|---|---|---|
| **M-08** (`C3`) | Config load crashes the app on I/O errors instead of reporting unhealthy. `AppConfigLoader.Load` calls `File.Exists`/`File.ReadAllText`, but only `WebConfigValidationException` is caught — so `IOException`, `UnauthorizedAccessException`, `PathTooLongException` and `DirectoryNotFoundException` escape DI singleton construction and kill startup. This contradicts the design intent stated three lines later and the health-check plumbing built for exactly this case: a file-permission problem on the mounted `config/` directory becomes an opaque boot-loop instead of a diagnosable unhealthy state. | Medium |
| **M-09** (`C4`) | `MaxConcurrentSessions` unvalidated. `new SemaphoreSlim(max, max)` throws on a negative value, so an app-setting typo crashes startup with a stack trace. `0` is worse: the app starts, reports **Healthy**, and silently refuses every session with "The server is at capacity." Validate `>= 1` in the config pipeline. | Medium |
| **L-06** (`C5`) | No reconnect backoff. `README.md` advertises "automatic reconnect" but the implementation is a manual **Reconnect** button — confirmed during this merge (see D-01). Holding the button opens a fresh socket and a fresh Azure session per click. Debounce it, and either implement backoff with jitter and a retry ceiling or correct the docs. | Low |
| **L-07** (`C6`) | `beforeunload` teardown cannot complete — `dispose()` awaits `audioContext.close()`, which the browser will not wait for during unload. Harmless in practice (the socket dies and `RequestAborted` fires) but the intent is unmet; `pagehide` with a synchronous `socket.close()` is the reliable idiom. | Low |

### Security

| ID | Finding | Sev |
|---|---|---|
| **M-10** (`S7`) | WebSocket origin check returns `true` when `Origin` is absent. Browsers always send it, so this is not browser-exploitable — but it converts the check from a control into a suggestion for any non-browser caller. Make the permissive branch opt-in (`VoiceLive:AllowMissingOrigin`, default `false`) so production fails closed. | Medium |
| **M-11** (`S8`) | CSP weaker than documented. Verified current value: `default-src 'self'; img-src 'self' data: blob:; media-src 'self' blob:; connect-src 'self' wss: https:; script-src 'self'; style-src 'self' 'unsafe-inline'; worker-src 'self' blob:`. Missing `frame-ancestors 'none'`, `base-uri 'none'`, `form-action 'self'` (relevant to H-02) and `object-src 'none'`; `connect-src ... wss: https:` permits exfiltration to **any** host when `'self'` would suffice; `'unsafe-inline'` is required only because `index.html` inlines all CSS. Also add `Cache-Control: no-store` on `/api/config` and authenticated HTML. | Medium |
| **L-08** (`S10`) | Unescaped `innerHTML` sink in `main.ts` startup error path. Interpolates a local `Error.message`, so not currently attacker-reachable — but it is the sole raw-HTML sink in an otherwise disciplined codebase, and it will be copied. Use `createElement` + `textContent`. | Low |

### Performance

| ID | Finding | Sev |
|---|---|---|
| **M-12** (`P2`) | Worklet computes and posts PCM even while muted. The worklet converts and transfers every 128-sample block unconditionally; the main thread then discards it. In `gated` mode — the **default** `activeMode` — the mic is idle almost always, yet at 24 kHz the worklet still performs ~187 conversions/second, allocates an `Int16Array` each time, and pays a `postMessage` + `ArrayBuffer` transfer for every one, all discarded. Continuous wasted CPU on a real-time audio thread. Push the gate into the worklet and `return true` early when not streaming. | Medium |
| **M-13** (`P3`) | Double allocation per inbound WebSocket frame on the audio hot path. The pooled 64 KB `ArrayPool` rental is good, but each frame then allocates a `MemoryStream` **and** a fresh `byte[]` via `ToArray()`, negating the pooling above it — sustained Gen0 churn on the server's hottest path. Fast-path the single-read case with `buffer.AsMemory(0, result.Count)`; use `GetBuffer()` or a pooled `ArrayBufferWriter<byte>` for multi-frame. | Medium |
| **L-09** (`P4`) | Bundle unminified and uncacheable — no `--minify`/`--target`, and `index.html` hardcodes `/app.js` with no content hash, forcing revalidation on every load and risking stale JS after deploy. | Low |
| **L-10** (`P5`) | B1 single instance hosting persistent WebSockets and continuous PCM marshalling, with no autoscale rule or `numberOfWorkers`. Reasonable as a demo footprint, but `SessionGate` is per-instance, so scale-out silently makes the cap *N × 2* — and the real constraint is avatar-rendering quota, not CPU. Document the tier as demo-scale, or pin `numberOfWorkers: 1`. | Low |
| **L-11** (`P6`) | `_sendLock` `SemaphoreSlim` never disposed. No handle leak today (`AvailableWaitHandle` is untouched) but latent as the class evolves. | Low |
| **L-12** (`P7`) | `getSample()` is a linear scan per output sample — O(chunks × outputSamples). Fine while `compact()` keeps `pending` at one or two chunks, but degrades quadratically on the audio thread if the queue ever grows. Flatten to a ring buffer with an absolute read index. | Low |

### Maintainability

| ID | Finding | Sev |
|---|---|---|
| **M-14** (`B1`) | Two overlapping authorization mechanisms. A hand-rolled middleware does the real gating via a hardcoded path-prefix list, while endpoints *also* carry decorative `.AllowAnonymous()` and `UseAuthorization()` runs afterwards. A future endpoint added with `.RequireAuthorization()` is gated by a string list in a different file that knows nothing about it; a new anonymous endpoint must remember to update that list. Note `StartsWithSegments("/logout")` also makes `/logout/anything` anonymous. Replace with `FallbackPolicy` + `.AllowAnonymous()` on the three public endpoints, and `OnRedirectToLogin` returning 401 for `/api` and `/ws`. *(The `UseStaticFiles`-after-gate ordering is correct and worth preserving.)* | Medium |
| **M-15** (`B2`) | Test-only public `TryGetControlType` duplicates the real parser. Nothing in production calls it; it exists for `ControlMessageTests` and reimplements the parse that `HandleControlMessageAsync` performs inline. Tests therefore validate a parallel copy while the real dispatcher — every `case`, the `say` handling, the turn-id lifecycle — has no direct coverage. Extract a genuinely testable seam and delete it. | Medium |
| **L-13** (`B5`) | Inconsistent namespace qualification in `Program.cs` — fully-qualified `VoiceLive.Web.Config.ConfigState` despite the `using`, adjacent to bare `SessionGate`. | Low |
| **L-14** (`B6`) | Brittle substring matching for avatar capacity errors (`"avatar"` + `"exhausted"`/`"capacity"`). An upstream wording change silently converts graceful voice-only degradation into a hard session failure. Match known error codes exactly, keep the heuristic as a documented fallback, and log at `Warning` when it fires so drift is visible. | Low |
| **L-15** (`B7`) | Single hardcoded API version — `Map()` accepts only `"2025-10-01"`, yet `VoiceLive__ApiVersion` is exposed as a runtime app setting, implying tunability. Misleading configuration surface. | Low |
| **L-16** (`B8`) | `ReadAvatarServer` enumerates the root JSON object four times with case-insensitive comparison, and `break` inside the loops means only the first matching property is examined in several. Enumerate once into a case-insensitive dictionary. | Low |
| **L-17** (`B9`) | No repo-wide build hygiene: no `Directory.Build.props` (`TreatWarningsAsErrors`, `EnableNETAnalyzers`, `LangVersion`), no `.editorconfig`, no `global.json` (CI floats on `10.0.x`), no `dotnet format --verify-no-changes`, and **CI never runs `dotnet publish`** — so the `BuildFrontend` target that produces the deployed artifact is never exercised. | Low |
| **L-18** (`B10`) | Undeclared Python 3 dependency in the JS test suite — `playwright.config.ts` shells out to `python3 -m http.server`. Works on `ubuntu-latest` and most dev machines; fails confusingly on clean Windows or minimal containers. Use a Node static server. | Low |
| **L-19** (`B11`) | `licence.md` is not detected as a license by GitHub's license API, `dotnet pack`, or SBOM/compliance tooling, all of which look for `LICENSE`/`LICENSE.md`/`LICENCE`. Confirmed: no `LICENSE` or `LICENSE.md` exists. Rename and reference from the README. | Low |
| **L-20** (`B12`) | Config read once at startup with no reload path — defensible for a kiosk (no session can observe half-applied config) but undocumented where an operator would look. | Low |

---

## A7. Where the reviews disagree

Recorded because the disagreements are informative, not noise.

| Topic | Opus | Sol | Resolution |
|---|---|---|---|
| Overall risk ceiling | 2 Critical, 4 High | No Critical or High | **Opus.** Both C-01 and H-01 were re-verified in code during this merge. Sol did not analyse the forwarded-headers interaction or the `say` trust boundary. |
| Error sanitization | Listed as a **strength** — `SafeError()` never leaks internals | Found one path that **does** leak upstream detail (L-01) | **Both correct.** The pattern is good; there is one inconsistent path. Sol's finding is narrower and more precise. |
| Autoplay | Not examined | Live defect in the primary unattended view (H-05) | **Sol.** Opus's most significant miss. |
| Rate limiter | Bypassable **and** the test proving it works is invalid (C-01, M-05) | Listed "Login requests are rate limited" as a confirmed strength | **Opus.** Sol validated the control's presence, not its integrity. |
| CI severity | Low | Medium | **Sol.** Absent gates are what let all other findings regress silently. |
| Audio resampling | Medium — Firefox/Safari clamp to 48 kHz, so it is a mainstream path | Low | **Opus**, on reach. |

**Methodological lesson:** Sol verified that controls *exist* (headers set, limiter registered, cookies flagged) and ran the full build/test/audit matrix. Opus verified that controls *hold* under an adversary and traced cross-file interactions (Bicep env var → middleware → limiter partition). Both passes are necessary; neither substitutes for the other.

---

## A8. Agreed strengths

Both reviewers independently identified these. Worth recording, because they are deliberate choices comparable projects routinely get wrong.

- **Credential isolation is correct.** No Azure key, token or endpoint secret ever reaches the browser. `DefaultAzureCredential` + managed identity + `disableLocalAuth: true` means there is no API key to leak. `/api/config` returns a curated `ClientConfig` projection, not the server config object.
- **Cancellation and teardown are genuinely well done.** `CreateLinkedTokenSource(requestAborted)`, `Task.WhenAny` → `cts.Cancel()` → `WhenAll(SwallowCancellation(...))`, `_sendLock` serializing sends from both pumps, `CloseIfOpenAsync` guarding socket state.
- **The client-side session-token pattern is excellent.** `isCurrentSession(token, socket)` guards every async continuation, so a reconnect during in-flight WebRTC negotiation or `getUserMedia` cannot corrupt the new session. With the idempotent `disconnectPromise`, this eliminates an entire class of WebRTC race conditions.
- **Configuration validation is thorough and aggregating** — errors accumulate into one actionable list, messages use a consistent `file: field: problem` form, and "invalid config → unhealthy but still running" is the mature choice. Strongly covered by tests.
- **XSS discipline in the view layer** — `views.ts` uses `createElement` + `textContent` throughout, including for server-supplied `safeQuestions` and transcripts. L-08 is the sole exception in ~440 lines.
- **Authentication fails closed** — unauthenticated `/api/*` and `/ws/*` return 401; HTML redirects to `/login`. Cookies are `HttpOnly`, `SameSite=Lax`, and `Secure` outside development.
- **Security headers and transport hardening** — CSP, frame denial, MIME-sniffing protection, referrer policy, HSTS, HTTPS-only, FTPS disabled, TLS 1.2 minimum. Forwarded-header processing is enabled on App Service, avoiding the reverse-proxy scheme mismatch that would otherwise break HTTPS redirects and same-origin checks.
- **Bounded and serialized WebSocket I/O** — 1 MiB inbound cap, serialized sends, reliable session disposal, gate released on exit.
- **The avatar-capacity voice-only fallback** is a thoughtful reliability feature with a genuinely helpful operator-facing message.
- **Observability is properly wired** — OpenTelemetry meters for active sessions, duration and errors; tagged error counters; `BeginScope` with a session id; health checks; Azure Monitor wired conditionally on the connection string.
- **Infrastructure follows least privilege** — system-assigned identity, two narrowly scoped role assignments, `httpsOnly`, `minTlsVersion: 1.2`, `ftpsState: Disabled`, `healthCheckPath` wired to the real endpoint.
- **`scripts/setup-agent.sh` is defensively written** — `set -euo pipefail`, GET-only by explicit design and documented as such, degrades gracefully without `jq` or env vars, always exits 0.
- **Dependency lock files and reproducible `npm ci` builds**, with a clean audit posture in both ecosystems.
- **Documentation is unusually complete for the project's size** — architecture diagrams, config schema reference, runbook, rehearsal checklist, preserved design history. *(See Part B for where that completeness diverges from accuracy.)*

---

## A9. Unified remediation plan

Merged from both prioritized lists, ordered by risk × effort.

### Gate 1 — before any exposed deployment

| # | ID | Action |
|---|---|---|
| 1 | **C-02** | Remove committed credentials; move to `dotnet user-secrets`; rotate the `testlab-f` resource if real; purge the published defaults from all four documents. |
| 2 | **C-01** | Configure `ForwardedHeadersOptions` explicitly with known proxies; partition on the validated IP; add a global `/login` backstop limiter and lockout/backoff. |
| 3 | **H-01** | Constrain `say` to a server-side allow-list or an index; add a length cap and per-connection rate limit. |
| 4 | **M-01** | Add absolute + activity-based idle session timeouts; emit a gate-rejection metric. |
| 5 | **M-02** | Move `Auth__Password` to a Key Vault reference. |
| 6 | **H-02** | Add antiforgery to `POST /login`. |
| 7 | **H-05** | Make autoplay failure recoverable — retry muted, then a gesture control; never tear down the session. |

### Gate 2 — correctness and trust boundaries

| # | ID | Action |
|---|---|---|
| 8 | **H-03**, **H-04** | Reconcile config validation, runtime capability and `docs/config-schema.md`; add a validated-set vs buildable-set drift test. |
| 9 | **M-08**, **M-09** | Make config-load I/O failures and a bad `MaxConcurrentSessions` produce unhealthy-with-message, not a crash or a silent always-full gate. |
| 10 | **M-14** | Replace the hand-rolled auth middleware with a `FallbackPolicy`. |
| 11 | **M-06** | Harden control-frame and server-frame validation on both sides; route client-side failures through `disconnect()`. |
| 12 | **M-15**, **M-05** | Make the real control-frame dispatcher testable; add integration coverage for origin, capacity, `say`, autoplay rejection, and per-IP limiter partitioning. |
| 13 | **M-10**, **M-11** | Make missing-`Origin` opt-in; tighten the CSP and align the README claim. |
| 14 | **L-01**, **L-02** | Sanitize the remaining upstream-error path; hash credentials before fixed-time comparison. |

### Gate 3 — performance, delivery and operations

| # | ID | Action |
|---|---|---|
| 15 | **M-04**, **M-12** | Fix worklet aliasing; gate PCM production while muted. |
| 16 | **M-13** | Remove per-frame allocations on the server audio path. |
| 17 | **M-03**, **L-17** | Add CI `permissions`, SHA-pinned actions, Dependabot, dependency + secret scanning, CodeQL, `az bicep build`, a `dotnet publish` job, `Directory.Build.props`, `.editorconfig`, `global.json`. |
| 18 | **L-09** | Minify and cache-bust the bundle. |
| 19 | **L-03**, **L-04**, **M-07** | Network restrictions, diagnostic settings to Log Analytics, and a real `AllowedHosts`. |
| 20 | **L-05** | Announce status changes to assistive technology. |
| 21 | **L-06**–**L-20** | Reconnect backoff/debounce, `pagehide` teardown, error-code matching, API-version mapping, `ReadAvatarServer` simplification, ring buffer, `IDisposable`, `LICENSE` rename, Node static server, docs accuracy. |

### Traceability

| Merged | Opus | Sol | | Merged | Opus | Sol |
|---|---|---|---|---|---|---|
| C-01 | S1 | — | | M-08 | C3 | — |
| C-02 | S2 | — | | M-09 | C4 | — |
| H-01 | S5 | — | | M-10 | S7 | — |
| H-02 | S4 | — | | M-11 | S8 | — |
| H-03 | C1 | — | | M-12 | P2 | — |
| H-04 | C2 | — | | M-13 | P3 | — |
| H-05 | — | MED-01 | | M-14 | B1 | — |
| M-01 | S6 | MED-04 | | M-15 | B2 | — |
| M-02 | S3 | MED-02 | | L-01 | — | LOW-02 |
| M-03 | S11 | MED-03 | | L-02 | — | LOW-05 |
| M-04 | P1 | LOW-04 | | L-03 | — | LOW-06 |
| M-05 | B3, B4 | *(supporting)* | | L-04 | — | LOW-08 |
| M-06 | B2 | LOW-01, LOW-03 | | L-05 | — | LOW-09 |
| M-07 | S9 | LOW-07 | | L-06…L-20 | C5, C6, S10, P4–P7, B5–B12 | — |

---
---

# Part B — Documentation alignment review

**New analysis, this pass.** Assessed against the five dimensions requested: **functional flow, technical architecture, the why, getting started, production deployment** — each measured against documentation best practice (Diátaxis, C4, ADRs, GitHub community-health standards, Azure Well-Architected operational-excellence guidance).

Every mismatch below was verified against the code at commit `d5110dc`.

## B1. Scorecard

| Dimension | Rating | One-line assessment |
|---|---|---|
| Functional flow | **Good** | Two strong Mermaid diagrams; missing the turn lifecycle and connection-state model — and the diagram that would fix it is already in the repo, orphaned. |
| Technical architecture | **Good** | Thorough component and endpoint inventory; no decision records, no threat model, wire protocol triplicated with no authoritative schema. |
| The *why* | **Weak** | A genuinely compelling rationale exists — in a file labelled "original design specification" that readers will assume is historical. The README never states it. |
| Getting started | **Fair** | Runnable in five commands, but it hands out real credentials, hardcodes a personal absolute path, omits two prerequisites, and never documents how to run the tests. |
| Production deployment | **Poor** | There is no production deployment documentation. `docs/runbook.md` is a *rehearsal* runbook. Secrets, identity, scale, cost, DR, alerting and data handling are all absent. |
| Documentation accuracy | **Poor** | **Six** verified claims describe behaviour the code does not implement. |
| Documentation hygiene | **Fair** | 91% of markdown is agent process history with no index; four community-health files missing; content triplicated and already drifting. |

## B2. Accuracy — documentation that contradicts the code

The most serious class of documentation defect: a reader who trusts these statements will make wrong decisions. All six verified this pass.

### D-01 — "Automatic reconnect" does not exist — **High**

`README.md` claims it **twice**:
> "Reliability features include manual turn gating …, **automatic reconnect**, health and error reporting…"
> "**Error and reconnect** — transient errors trigger **automatic reconnect with backoff**; fatal errors surface in the operator view."

**Verified:** `views.ts` creates a manual `reconnectButton` in all three views (lines 106, 321, 402), wired to `setReconnectHandler` and unhidden on disconnect. There is no timer, no retry loop, and no occurrence of `backoff` anywhere in the frontend. Reconnection is entirely operator-initiated.

This is the single most consequential doc defect, because it is a **reliability** claim in a project whose own specification states "reliability and rehearsability beat features." An operator who reads the README will not staff a person to click Reconnect. Cross-references Opus `C5` / **L-06**.

### D-02 — Autoplay: docs promise a banner, code destroys the session — **High**

`docs/runbook.md` §7:
> "If the browser blocks autoplay, **the UI shows a clear banner asking the operator to interact with the page**."

**Verified:** any non-`AbortError` rejection from `play()` calls `void this.disconnect(...)`, terminating the whole Voice Live session. A banner does appear — as a *fatal error* banner, after the session is gone. `docs/rehearsal-checklist.md` is accurate on this point ("If an avatar/session error appears, the session has closed; reload/restart the tab"), so the two documents contradict each other. Directly cross-references **H-05**.

### D-03 — `agent.json` keys documented as required and fail-fast are never read — **High**

`docs/config-schema.md` marks `conversationResumePolicy` and `groundingStrategy` **Required** with enumerated allowed values, and asserts under Validation rules:
> "Unknown values for `voice.type`, `turntaking.activeMode`, **`agent.groundingStrategy`, or `agent.conversationResumePolicy` fail fast at startup**."

**Verified:** a repository-wide search across all `.cs` and `.ts` files returns **zero** references to `groundingStrategy`, `conversationResumePolicy`, or `agentVersion`. Nothing reads them; nothing validates them; no value of any of them changes any behaviour. The documentation describes a validation feature that does not exist. Same root cause as **H-04**.

### D-04 — `azure-custom` documented as supported, guaranteed to fail — **Medium**

`docs/config-schema.md` lists `azure-custom` among allowed `voice.type` values in both the field table and the validation rules. **Verified:** accepted by `WebConfig.cs:22`, then thrown by `SessionOptionsBuilder.cs:55` on every session start. An operator following the schema reference gets a Healthy app and zero working sessions. Same root cause as **H-03**.

### D-05 — "A strict Content-Security-Policy" — **Medium**

`README.md` describes the CSP as *strict*. **Verified** actual value permits `connect-src 'self' wss: https:` — outbound connections to **any** HTTPS or WSS host — and `style-src 'unsafe-inline'`, while omitting `frame-ancestors`, `base-uri`, `form-action` and `object-src`. Calling this strict discourages the hardening in **M-11**.

### D-06 — Published working credentials in four documents — **Medium**

`operator` / `rehearsal` appears in `README.md`, `web/README.md`, `docs/runbook.md` §6, **and** `docs/config-schema.md` (as "Development default"), plus `appsettings.Development.json`. Documentation is the amplifier that turns C-02 from a committed-file problem into a published-credential problem. Best practice is `dotnet user-secrets` with the docs describing how to set them, never what they are.

## B3. Functional flow

**Present and good.** `README.md` §"How it works" gives a Mermaid `flowchart LR` with correct trust-boundary subgraphs, an explicit two-path explanation (control+audio relay vs. direct WebRTC media), a `sequenceDiagram` covering sign-in → WS upgrade → token → session config → ICE → SDP exchange → first turn, and complete bidirectional frame-vocabulary tables. The failure-mode table (`invalid config`, `429`, `403`, capacity, oversize, service error, avatar capacity) is exactly the right artifact for an operator.

**Gaps:**

### D-07 — Three flow diagrams are tracked but referenced by nothing — **Medium**

`docs/images/` contains `voice_live_single_turn_flow.png`, `voice_live_prewarm_connection_flow.png` and `voice_live_decision_points.png`. **Verified:** zero markdown files in the repository reference any of the three. They are orphaned binary assets — and by their filenames they are precisely the three flows the documentation is missing. Either wire them into the README/runbook or delete them; tracked-but-unreferenced images rot silently and mislead the next reader into thinking flow documentation exists.

### D-08 — No turn lifecycle or connection-state documentation — **Medium**

The frame tables list `start-turn`, `end-turn`, `barge-in`, `say`, `response-done` individually, but no document shows the **sequence and legal transitions**: what happens if `end-turn` arrives without `start-turn`, when barge-in is valid, how `gated` differs from `hybrid` at runtime, or what the operator should see at each step. Likewise the UI exposes six independent status channels (connection, WebRTC, mic, turn, speech, avatar) and no document enumerates their states or which combinations mean "healthy". An operator diagnosing a stuck session mid-show has no reference. A single state diagram plus a status-value table would close this.

### D-09 — The three views are documented as a URL table, not as journeys — **Low**

Each view gets one line. Nothing states which controls exist in which view, that `?view=display` has **no microphone**, that the ⚙ gear is the only route from landing to operator, or that each view opens an independent session consuming a concurrency slot — the last of which interacts directly with **M-01** and is the most likely real-world operator surprise (open landing + display = capacity exhausted at `MaxConcurrentSessions=2`).

## B4. Technical architecture

**Present and good.** `README.md` §Architecture covers the composition root, an endpoint table with auth requirements, the config directory layout, `SessionOptionsBuilder` responsibilities, the bridge's two pumps, the browser client structure, trust boundaries, observability, failure behaviour, and the Azure topology. `docs/initial-spec.md` adds platform facts about Voice Live itself.

**Gaps:**

### D-10 — No architecture decision records; the "why" of each choice is unreachable — **Medium**

The architecture is documented as a *description* of what exists, not as a set of decisions with rationale and rejected alternatives. Why cookie auth rather than Entra ID / Easy Auth? Why is the media plane WebRTC-direct instead of relayed? Why `MaxConcurrentSessions = 2`? Why is config startup-only (Opus **L-20**)? Why B1? Each has a real answer that a maintainer needs, and none is written down where it will be found. `docs/superpowers/specs/*` contains much of this reasoning, but those are dated agent design documents, not decision records, and are indexed nowhere (see D-15). A short `docs/adr/` set — or a "Key decisions" README section with a rationale column — would make the design defensible six months out.

### D-11 — Wire protocol triplicated with no authoritative schema — **Medium**

The frame vocabulary and endpoint table are documented in `README.md`, `web/README.md` **and** `docs/initial-spec.md`, with no single source of truth. Frame *names* are listed but frame *shapes* are not: no document specifies that `ready` carries `config`, `safeQuestions` and ICE servers, nor which fields are required. That absence is the documentation half of **M-06** — you cannot validate a contract that was never written. Drift has already begun: `README.md` says the managed identity gets `Cognitive Services User` and `Foundry User`, while `docs/runbook.md` and `docs/rehearsal-checklist.md` say `Cognitive Services User` plus `Foundry User` / `Azure AI User`. A reader cannot tell which is correct.

### D-12 — No threat model or trust-boundary document — **Medium**

Trust boundaries are described in prose ("the browser never receives an Azure token") but there is no enumeration of actors, assets, entry points and assumed-trusted components. Both merged reviews found their most severe issues precisely where an unstated assumption failed: that `RemoteIpAddress` is trustworthy (**C-01**) and that an authenticated client is a benign client (**H-01**). A one-page threat model listing "authenticated user is trusted to send arbitrary `say` text — **accepted risk / not accepted**" would have surfaced both before code review.

## B5. The *why*

### D-13 — The strongest rationale in the repository is in the one file readers will skip — **High**

`docs/initial-spec.md` §1 contains an excellent, decision-shaping problem statement:

> "the avatar converses **on stage with a C-level leader**, explaining the direction of the company, in front of a live audience. This may happen in a **noisy environment**." → "**Reliability and rehearsability beat features.** Anything that can fail mid-show needs a defined behavior and an operator control."

That single paragraph explains hold-to-talk, safe questions, the voice-only fallback, deep noise suppression, the operator console, the rehearsal checklist, and the "explicit failures, never masked" stance. It is the *why* of the entire repository.

But `README.md` never states it. The README opens with *what* ("A stage-ready conversational avatar built on Microsoft Foundry Voice Live") and proceeds straight to mechanism. And the README's own reference list labels the file **"original design specification"** — signalling *historical*, which readers reasonably skip. The rationale is present but effectively unreachable.

Also absent everywhere: **non-goals**, target audience, and a "when *not* to use this" section. Both reviews conclude the app is unsuitable for untrusted or internet-facing users; no document says so, which is itself the highest-impact documentation gap in the repository (see D-14).

**Recommendation:** Lift the use case and the "consequences that shape every decision" list into the README's opening section. Add explicit non-goals (not multi-tenant, not multi-user, not internet-facing, one session per tab). Re-label `initial-spec.md` as historical *or* promote it to a maintained design document — but do not leave it as the sole home of the project's rationale.

## B6. Getting started

**Present and largely good.** Prerequisites are listed, the quickstart is five commands, the MSBuild `BuildFrontend` behaviour is explained so the missing `npm` step is not a surprise, a `curl /api/health` verification step is included, and `DefaultAzureCredential` behaviour and the avatar-capacity fallback are both explained inline.

**Gaps beyond D-06 (published credentials):**

### D-14 — No "how to run the tests" anywhere — **Medium**

The repository has 90 backend tests, 23 Playwright tests, and a TypeScript check, all wired into CI. **No document names `dotnet test`, `npm test`, or the type-check command.** A contributor cannot discover how to validate a change without reading `.github/workflows/ci.yml`. Compounding this, the Playwright suite silently requires **Python 3** on `PATH` (`playwright.config.ts` shells out to `python3 -m http.server`) and Python appears in no prerequisite list — Opus **L-18**. Add a "Development" section covering test commands, the type check, and every prerequisite the test suite actually needs.

### D-15 — `web/README.md` hardcodes a personal absolute path — **Low**

```bash
ConfigDir=/home/jbergfeld/vcs/foundry-voice-live-avatar/config ASPNETCORE_URLS=http://127.0.0.1:5210 dotnet run ...
```

A copy-paste command containing one contributor's home directory. Not portable, and it leaks local filesystem layout. Use `ConfigDir=./config` or `$(pwd)/config`.

### D-16 — No prerequisite verification, and `session.sample.json` is never explained — **Low**

The quickstart does not show how to confirm the environment is actually ready — no `dotnet --version` / `node --version` check, and no `az role assignment list` to verify the two roles the same section says are required. Given that missing RBAC is the top troubleshooting row in the runbook, a verification step would prevent the most common first-run failure. Separately, `config/session.sample.json` is tracked and mentioned in the README's config listing as "sample session config excluded from publish", but no document explains when or why a user would copy it.

## B7. Production deployment

**This is the largest gap between the documentation set and the user's stated expectation.**

What exists is good for what it is: `docs/runbook.md` §3 covers `azd up`, region rationale (`swedencentral` for native realtime + avatar + agent mode — genuinely useful and non-obvious), the resources provisioned, the `LINUX_FX_VERSION` fallback for regions lacking `DOTNETCORE|10.0`, RBAC, agent-mode opt-in, failure handling, and a nine-row troubleshooting table. `docs/rehearsal-checklist.md` is an excellent pre-show operational artifact.

But the runbook is a **rehearsal** runbook. Measured against Azure Well-Architected operational excellence, there is **no production deployment documentation at all**. Every item below is entirely absent:

### D-17 — No hardening or readiness statement — **High**

Both merged reviews independently concluded the app is not ready for untrusted or internet-facing users. **No document says this.** A reader following `README.md` §"Deploy to Azure" gets a public HTTPS endpoint protected by one shared password, with a documented-as-effective rate limiter that **C-01** shows is bypassable. The absence of a "before you expose this" checklist is the highest-severity documentation finding in this review, because the docs actively invite the deployment the reviews warn against.

### D-18 — Secret and identity management undocumented for production — **High**

The documented path is `azd env set AUTH_PASSWORD <password>`, which lands the sole operator credential in plaintext App Service settings (**M-02**). No document mentions Key Vault, rotation, or who can read app settings. More fundamentally, a **single shared static credential** is presented as *the* authentication model with no production alternative — no mention of Entra ID, App Service Easy Auth, or per-operator identity, which is the standard answer for this application shape. A production deployment guide must state the intended identity model and its limits.

### D-19 — No capacity, scale or cost guidance — **High**

- **Scale:** B1 single instance is stated as a fact, never as a constraint. No document warns that `SessionGate` is a per-instance semaphore, so scaling out silently multiplies the concurrency cap (**L-10**) — an operator "fixing" capacity by scaling out would break the control without any signal.
- **Quota:** avatar-rendering quota is the actual binding constraint and appears **only** as a troubleshooting row, not as capacity planning. Nothing tells a reader to request quota *before* an event.
- **Cost:** **no cost guidance exists anywhere**, for a service billed per session-minute, in an app with no session timeout (**M-01**). Two forgotten tabs bill indefinitely, and no document warns of it. A production guide must state the cost model, the drivers, and the guardrails.

### D-20 — No day-2 operations: alerting, SLOs, DR, or promotion path — **High**

| Missing | Why it matters here |
|---|---|
| Alert rules / KQL / dashboards | App Insights and Log Analytics are provisioned and OpenTelemetry metrics are emitted, but **no document names a single alert, query, or dashboard**. `/api/health` is documented without saying what to page on. Telemetry nobody looks at is not observability. |
| SLOs / error budget | No target availability or latency, so there is no definition of "working". |
| Environments & CD | No dev/test/prod promotion model, no deployment slots, no CD pipeline. CI never deploys and never runs `dotnet publish`, so the artifact-producing path is untested (**L-17**) — undocumented and unexercised. |
| Rollback | No rollback or redeploy-previous-version procedure. For a live event this is the single most important production procedure and it is absent. |
| BCP / DR | No answer to "the region is degraded 30 minutes before showtime." Given the whole project exists to serve one high-stakes live moment, a documented fallback (second region pre-provisioned, voice-only mode, pre-recorded content) is the highest-value missing runbook entry. |
| Networking | No custom domain, TLS certificate, private endpoint / VNet integration (**L-03**), or Front Door / WAF guidance. |
| Data handling & privacy | **Nothing states whether microphone audio or transcripts are persisted, logged, or retained**, or what Azure does with them. Mandatory for any voice-processing system, and a GDPR-relevant gap for a `swedencentral` deployment. |
| Config change procedure | Config is startup-only with no reload path (**L-20**); no document tells an operator that editing `config/` requires a restart — a live-event footgun. |

### D-21 — Point-in-time test evidence embedded in the runbook — **Low**

`docs/runbook.md` §7 states: *"A headless browser E2E reached WebRTC `connected` state with video and audio tracks arriving, and the safe-question path produced streaming transcripts plus a completed response."* This is a test result from a specific moment, not operational guidance, and it will silently become false. Move it to a test report or release note; keep the runbook to current, verifiable procedure.

## B8. Documentation hygiene

### D-22 — 91% of markdown is agent process history, with no index — **Medium**

**Verified line counts:** of 9,350 tracked markdown lines, **8,519 (91%)** are `docs/superpowers/` plans and specs (8 plans totalling 6,925 lines; 8 specs totalling 1,594). Current operator-facing documentation — `README.md`, `web/README.md`, and the four `docs/*.md` files — is **795 lines, 8.5% of the total.**

There is **no `docs/README.md`** to tell a reader which files are current and which 16 are historical. Worse, `docs/runbook.md` §1 links a dated spec as "the design background", implying the process history is normative reference material. A newcomer landing in `docs/` cannot distinguish a maintained reference from a completed work order.

Preserving agent plans is legitimate and often valuable. Presenting them unlabelled alongside operational documentation is not.

**Recommendation:** Add `docs/README.md` indexing the maintained set, and move process history under `docs/history/` (or add a clear "historical, not maintained" banner to each). Consider a Diátaxis split — tutorial (quickstart), how-to (runbook, checklist), reference (config schema, wire protocol), explanation (why, ADRs) — which maps almost perfectly onto what already exists plus the gaps above.

### D-23 — Four community-health and compliance files missing — **Medium**

**Verified absent:** `SECURITY.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `CHANGELOG.md`. `.github/` contains only `workflows/`.

`SECURITY.md` is the notable one: this repository has just received two independent reviews finding Critical-severity issues, and there is **no documented channel to report a vulnerability**. `CONTRIBUTING.md` is the natural home for the missing test commands (D-14) and build hygiene expectations. And `licence.md` will not be detected as a license by GitHub's license API, `dotnet pack`, or SBOM/compliance tooling (**L-19**) — confirmed: neither `LICENSE` nor `LICENSE.md` exists.

### D-24 — Triplicated content is already drifting — **Medium**

The endpoint table, frame vocabulary, security model, local-run instructions, dev credentials, and the "each tab opens its own session" limitation each appear in three or four documents. Confirmed drift: the RBAC role names differ between `README.md` and `docs/runbook.md` / `docs/rehearsal-checklist.md` (D-11). Every future change to any of these facts requires three or four correct edits — which is exactly how D-01 through D-05 arose. Establish one authoritative location per fact and link to it.

## B9. Documentation remediation plan

**Fix first — documentation that will cause wrong decisions**

1. **D-01, D-02, D-03, D-04, D-05** — correct all five false claims, or fix the code to match (H-03, H-04, H-05, L-06, M-11 already cover the code side). Prefer fixing code for D-02/D-03; prefer fixing docs for D-01/D-05.
2. **D-17** — add a "Not production-ready without these steps" section to the README, with the Gate 1 list from A9.
3. **D-06** — purge published credentials from all four documents; replace with `dotnet user-secrets` instructions.
4. **D-13** — lift the use case, design consequences and explicit non-goals into the README opening.

**Then — close the production gap**

5. **D-18, D-19, D-20** — write `docs/production-deployment.md`: identity model and limits, Key Vault secret handling, capacity and avatar-quota planning, cost model and guardrails, alert rules with KQL, SLOs, environment promotion and CD, rollback, DR/BCP for event day, networking and custom domain, data handling and retention, config-change procedure.
6. **D-14, D-15, D-16** — add a Development section (test commands, type check, Python prerequisite); fix the hardcoded path; add prerequisite verification.

**Then — structure and durability**

7. **D-22** — add `docs/README.md`; relocate or clearly label process history.
8. **D-11, D-24** — single authoritative wire-protocol reference including frame schemas; de-duplicate the rest and reconcile the RBAC role names.
9. **D-10, D-12** — add ADRs (or a decisions table) and a one-page threat model.
10. **D-07, D-08, D-09** — wire in or delete the orphaned diagrams; document the turn lifecycle, connection-state model and per-view journeys.
11. **D-23** — add `SECURITY.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `CHANGELOG.md`; rename `licence.md` → `LICENSE.md`.
12. **D-21** — move point-in-time test evidence out of the runbook.

## B10. What the documentation does well

Genuinely above average for a project this size, and worth preserving through any restructuring:

- **Two accurate, well-chosen Mermaid diagrams** — a component/trust-boundary flowchart and a session-startup sequence — kept in-repo as text, so they are diffable and reviewable.
- **The two-path explanation** (control relay vs. direct WebRTC media) is the single most important thing to understand about this architecture, and the README leads with it.
- **Explicit failure-mode tables** in both `README.md` and `docs/runbook.md`, enumerating behaviour rather than promising success — this matches the codebase's actual "fail visibly" design.
- **The region rationale** (`swedencentral` for native realtime + avatar + agent mode; West Europe insufficient) is non-obvious, hard-won, and exactly what a deployment doc should capture.
- **The `LINUX_FX_VERSION` workaround** documents a real-world Azure constraint with a concrete remedy.
- **A nine-row troubleshooting table** mapping symptom → likely cause → operator action, including the genuinely obscure `avatar_service_resource_exhausted` quota case.
- **`docs/rehearsal-checklist.md`** is a model operational artifact: day-before, event-day, during-show, and stakeholder-briefing sections, with known limitations stated plainly rather than hidden.
- **Known limitations are documented honestly** — "each browser tab opens its own session", "hosted tools may not emit a client event", "shared operator+display room is future work". Many projects omit these.
- **`docs/config-schema.md`** is a thorough field-by-field reference with types, requiredness, defaults and validation rules — the right artifact, undermined only by D-03 and D-04.
- **`docs/initial-spec.md` §1** is an unusually clear articulation of use case → consequences → architecture. It deserves a far more prominent home.
