# Design: Run the web app on App Service with managed identity, deployed via azd (CLI deprecated)

Date: 2026-07-23
Tracks: GitHub issues #1–#12 (epic #12). Single PR / single branch `feature/appservice-managed-identity-epic`.

## 1. Goal

Move `foundry-voice-live-avatar` from "trusted operator machine, no web auth" (current README threat
model) to the decided end state:

- The **web app** (`web/src/VoiceLive.Web`) is the single product; the **CLI is removed**.
- Hosted on **Azure App Service (Linux)**, authenticating to Foundry via a **system-assigned managed
  identity** — no secrets in code or app settings.
- Provisioned and deployed with **azd** (`azd up`), including a freshly provisioned Foundry
  account + project + model deployment + persistent agent.
- Authenticated with an **application-level username/password login** (cookie auth) because the target
  environment cannot create an Entra app registration (rules out App Service Easy Auth / Entra OIDC).

The work is delivered as one PR with commits grouped by the four phases below so review stays tractable.

## 2. Scope & non-goals

**In scope:** all 12 issues.

**Non-goals:**
- No shared code library between apps (there is only one app after the CLI is removed).
- No VNet/private endpoints (public access with app-level auth + managed identity is the model).
- No custom domain / TLS cert management (App Service default hostname + platform TLS).
- No Key Vault for the login secret in v1 (App Service app setting; documented, and Key Vault is a
  documented follow-up — the tenant's KV policies make KV brittle here).

## 3. End-state architecture

```
Browser ──HTTPS──> App Service (Linux, .NET 10)
  │  login form (cookie auth)        │
  │  GET / , /api/config             │  System-assigned managed identity
  └─ WSS /ws/session ────────────────┤     │
                                     │     └── Entra token (https://ai.azure.com/.default)
                                     ▼
                        Azure AI Foundry account (AIServices, Sweden Central)
                          ├─ project  proj-default
                          ├─ deployment gpt-4o-mini (agent model)
                          └─ persistent agent  company-direction-avatar
                        Voice Live data plane (avatar + realtime, no model deployment needed)
                        App Insights (workspace-based) <── OpenTelemetry
```

Provisioning: `main.bicep` (subscription scope) → resource group → AI Foundry account/project/deployment,
App Service plan + site, Log Analytics + App Insights, role assignments. `azd` `postprovision` hook
creates the persistent agent via the Foundry data-plane REST API (no Bicep resource exists for agents).

## 4. Phase 1 — Security (code only)

### #3 Remove the unauthenticated `/api/token`
- Delete the `MapGet("/api/token", …)` endpoint (`Program.cs:54-65`) and the whole `Tokens/` folder:
  `EntraTokenBroker.cs`, `ITokenBroker.cs`, `TokenBrokerException.cs` (and `AccessTokenResult` if present).
- Delete `TokenEndpointTests`.
- Update `web/README.md` (endpoint list + Security section) and root `README.md`.

### #4 Authentication + Origin validation + session limits
- **Auth (revised from Easy Auth):** ASP.NET Core **cookie authentication** with a minimal login form.
  - `GET /login` serves a small HTML form; `POST /login` validates username + password against
    configuration (`Auth:Username`, `Auth:Password`) using a fixed-time comparison, then signs in a
    cookie principal. `POST /logout` clears it.
  - A **fallback authorization policy** (`RequireAuthenticatedUser`) protects everything except
    `/login`, `/logout`, `/api/health`, and the login page's own static assets.
  - The `/ws/session` upgrade is protected by the same cookie (browsers send same-origin cookies on WS
    handshakes), so no separate WS auth is needed.
  - Credentials come from config/app settings (never committed except dev defaults in
    `appsettings.Development.json`). Bicep sets `Auth__Username` / `Auth__Password` app settings.
- **Origin validation:** on the `/ws/session` upgrade, reject when the `Origin` header is not in a
  configurable allowlist (defaults to the request host; local dev origins configurable). Closes CSWSH
  even though a valid cookie is present.
- **Session limits:** a `SemaphoreSlim`-based concurrent-session gate (configurable cap, default 2).
  When full, accept the socket, send `{t:"error", …}`, and close — covered by a test.
- **Rate limiting:** `AddRateLimiter` with a fixed-window limiter on the HTTP endpoints (defense in depth).
- Update both READMEs' trust model.

### #5 Harden the WebSocket bridge
1. **Message size cap:** in `PumpBrowserMessagesAsync`, cap the assembled `MemoryStream` (default 1 MB);
   on exceed, close with `WebSocketCloseStatus.MessageTooBig` and stop the pump (don't kill via exception).
2. **Malformed JSON:** wrap `JsonDocument.Parse` in `HandleControlMessageAsync` in `try/catch
   (JsonException)`; log at debug and ignore the frame (optionally reply an error frame). Missing/invalid
   `sdp`/`text` shapes are already guarded by `TryGetProperty`; keep them defensive.
3. **Keep-alive:** configure `WebSocketOptions.KeepAliveInterval` (e.g. 30 s) in `Program.cs` so idle
   sessions survive App Service's ~230 s idle timeout regardless of the page timer.
4. **HTTP hardening:** `UseHsts` + `UseHttpsRedirection` (prod), security headers middleware
   (`X-Content-Type-Options: nosniff`, a basic CSP for the static page, `Referrer-Policy`,
   `X-Frame-Options`), and `AllowedHosts` set per environment (not `*` in prod).
5. **`ping`:** reply with `{t:"pong"}` so the client can detect a dead bridge.

## 5. Phase 2 — Platform readiness (code only)

### #2 Single DI-registered `TokenCredential`
- Register one `TokenCredential` singleton: `DefaultAzureCredential` with
  `ManagedIdentityClientId = AZURE_CLIENT_ID` when that setting is present (supports user-assigned MI);
  otherwise the default chain (local `az login`, system-assigned MI on App Service).
- Inject it into the bridge and pass it to `VoiceLiveClient` (replaces the per-connection
  `new DefaultAzureCredential()` at bridge line ~30).
- Make the bridge DI-managed: register a `VoiceLiveWebSocketBridge` **factory** (the bridge needs a
  per-connection `ServerSessionConfig` + logger + credential); the `/ws/session` endpoint resolves the
  factory instead of `new`-ing the bridge. One credential instance for the app lifetime; tokens are cached.
- README: document the RBAC requirement (role on the AI Services account for Voice Live data-plane).

### #6 Externalize environment config + make `config/` deployable
- **Environment settings** (`endpoint`, `mode`, `apiVersion`) move to ASP.NET configuration
  (`appsettings.json` + env/app settings), bound with the options pattern to a `VoiceLiveOptions`.
  App Service app settings (set by Bicep) override. Remove the committed `testlab-f` endpoint from
  `config/session.json` (replace with a placeholder / `session.sample.json`; `endpoint` no longer lives
  in the show-tunable file).
- **Show tunables** (`voice`, `turntaking`, `avatar`, `agent`, grounding) stay in `config/*.json`, but
  are included in publish output via `<Content Include="..\..\..\config\**\*"
  CopyToPublishDirectory="PreserveNewest" Link="config\%(RecursiveDir)%(Filename)%(Extension)" />` so the
  default `ConfigDir=config` works both locally-from-project and when deployed.
- **Load + validate once at startup:** parse/validate config during startup, register the validated
  model as a DI singleton, fail fast with the aggregated error list. `/api/config` and `/ws/session`
  read the singleton instead of re-reading disk per request. (Ties into #11 health check.)

### #9 Build the frontend during publish; stop committing the bundle
- `git rm --cached web/src/VoiceLive.Web/wwwroot/app.js` (and `.map` if tracked). Keep the `.gitignore`
  entries (they start working once untracked).
- Add an MSBuild `Exec` target to `VoiceLive.Web.csproj` that runs `npm ci` + `npm run build` in
  `web/frontend` before build/publish, with `Inputs`/`Outputs` for incremental caching, so `dotnet run`,
  `dotnet publish`, CI, and `azd up` all get a fresh bundle. Guard with a condition so it can be skipped
  (`-p:SkipFrontendBuild=true`) when node is unavailable.
- `package.json`: real `name`/`description`, keep `private: true` (the repo ships no npm package and has
  no LICENSE file, so drop the misleading default `ISC`), add `"typecheck": "tsc --noEmit"`, remove the
  failing placeholder `test` script (or make it a no-op).
- Decide `wwwroot` story: `index.html` and `pcm-worklet.js` are hand-authored and stay in `wwwroot`;
  document that `wwwroot` is mixed (generated `app.js` + static assets). (No move of static files in v1.)

## 6. Phase 3 — azd + operations

### #1 azure.yaml + Bicep (`infra/`)
- **`azure.yaml`** at repo root: one service `web`, `project: web/src/VoiceLive.Web`, `language: dotnet`,
  `host: appservice`. A **`prebuild` hook** runs `npm ci && npm run build` in `web/frontend` (posix +
  windows variants). A **`postprovision` hook** runs `scripts/create-agent.sh` (agent creation).
- **`infra/main.bicep`** (subscription scope, `azd`-style with `resourceToken`), modules under
  `infra/core/` or inline:
  - AI Foundry account: `Microsoft.CognitiveServices/accounts@2025-06-01`, kind `AIServices`,
    `allowProjectManagement: true`, `customSubDomainName`, `SystemAssigned` identity,
    `disableLocalAuth: true`.
  - Project: `Microsoft.CognitiveServices/accounts/projects@2025-06-01` (`proj-default`).
  - Model deployment: `Microsoft.CognitiveServices/accounts/deployments@2025-06-01` — `gpt-4o-mini`,
    SKU `GlobalStandard`, for the agent. (Voice Live model-mode needs **no** deployment.)
  - App Service plan (`Microsoft.Web/serverfarms`, Linux, `B1` default / `P0v3` note) + site
    (`Microsoft.Web/sites`, `linuxFxVersion: DOTNETCORE|10.0`, `webSocketsEnabled`, `alwaysOn`,
    `httpsOnly`, `minTlsVersion: 1.2`, `healthCheckPath: /api/health`, app settings). System-assigned MI.
  - Log Analytics + workspace-based App Insights; connection string into
    `APPLICATIONINSIGHTS_CONNECTION_STRING`.
  - Role assignments to the site MI: `Cognitive Services User`
    (`a97b65f3-24c7-4388-baec-2e87135dc908`) on the account + `Foundry User`
    (`53ca6127-db72-4b80-b1b0-d745d6d5456d`) on the project.
  - App settings: `ConfigDir=config`, `VoiceLive__Endpoint`, `VoiceLive__Mode`, `VoiceLive__ApiVersion`,
    `Auth__Username`, `Auth__Password`, `VoiceLive__AllowedOrigins`, `ASPNETCORE_ENVIRONMENT=Production`.
  - Outputs azd needs for the postprovision hook: account name, project name, project endpoint, web app
    name/uri.
- **.NET 10 runtime risk:** if `DOTNETCORE|10.0` is not offered, fall back to a **self-contained**
  publish (`-r linux-x64 --self-contained`) with empty `linuxFxVersion`. Decide at deploy time based on
  what the platform accepts; the Bicep parameterizes `linuxFxVersion`.

### #1 Agent provisioning (postprovision)
- `scripts/create-agent.sh`: idempotent — GET the project's agents; if an agent named
  `company-direction-avatar` exists, reuse its id; else POST to create one referencing `gpt-4o-mini`,
  with instructions sourced from `config/grounding/company-direction.md`. Uses
  `az rest --resource https://ai.azure.com`. Writes `azd env set` for the resolved agent name/id.
- **Endpoint-path verification (no fabricated APIs):** before trusting `/assistants` vs `/agents`, verify
  the correct data-plane path live against the existing `testlab-f/proj-default` project (prior evidence:
  `GET {projectEndpoint}/agents?api-version=v1` returned 200). The script tries the verified path and
  falls back to the other on 404. If neither can be verified, the hook logs a clear message and the agent
  becomes a **documented manual step** rather than a fabricated call.

### #11 Observability + real health check (code)
- Add `Azure.Monitor.OpenTelemetry.AspNetCore` (`UseAzureMonitor()`), reads
  `APPLICATIONINSIGHTS_CONNECTION_STRING`. Console logging stays for local.
- Replace the static `/api/health` with ASP.NET **health checks**: a readiness check that the validated
  config is present (startup singleton) and optionally that a token can be acquired (cached so the probe
  doesn't hammer IMDS). Map `/api/health`; unhealthy → 503. App Service health path points at it.
- Per-connection **session id** log scope in the bridge (correlates concurrent sessions), plus simple
  metrics: active sessions (up/down counter), session duration (histogram), Voice Live errors by code.

### #7 Fix CI
- Move `pipeline/ci.yml` → `.github/workflows/ci.yml`; delete `pipeline/`.
- `frontend` job: `npm ci` + `npx tsc --noEmit` (typecheck). The bundle-drift guard is dropped because
  the bundle is no longer committed (#9). Drop the `cli` job (#8). `web` job unchanged
  (`dotnet test web/VoiceLive.Web.sln`).
- Follow-up (documented, not built now): an azd deploy workflow with OIDC federated credentials.

## 7. Phase 4 — Consolidation

### #8 Remove the CLI + doc sweep
- `git rm -r cli/` (source, tests, `VoiceLive.Cli.sln`).
- Docs: root `README.md` (drop "two independent apps" framing and the phantom `/tools/sync-agent`
  reference), `docs/runbook.md`, `docs/rehearsal-checklist.md`, `docs/config-schema.md` (remove CLI
  usage; config-schema keeps field docs, now web-only). `config/` stays.

### #10 Code-quality cleanup
- **Unify config loaders:** one parse+validate path producing a single validated model; project the
  browser-safe `ClientConfig` from it (removes the duplicate `Read`/`Require` helpers, the two
  `JsonSerializerOptions`, and the diverging validation between `WebConfigLoader.Load` and
  `LoadServerSession`). Update `ConfigEndpointTests` / `ServerSessionConfigTests`.
- **Model-mode instructions from config:** replace the literal `"You are a helpful assistant. Reply in
  concise, spoken sentences."` (bridge `RunAsync`) with the contents of
  `config/grounding/company-direction.md` (path configurable), so model-mode rehearsal reflects the show
  persona.
- **`model` required only in model mode:** the unified validator requires `session.model` only when the
  resolved mode is `model` (agent mode owns the model).
- **Validate `apiVersion` at startup:** `VoiceLiveServiceVersionMapper` no longer silently falls back;
  an unknown `apiVersion` is a startup validation error (fail fast) — or the set of supported versions is
  documented and enforced.
- **Testability (optional):** extract the Voice Live update→browser-frame mapping into a pure translator
  so the wire protocol is unit-testable. Time-boxed; only if it doesn't balloon the PR.

## 8. Testing & verification strategy

- **Build/test:** `dotnet build web/VoiceLive.Web.sln` and `dotnet test web/VoiceLive.Web.sln` (existing
  tests updated; new tests for session cap, message-size cap, malformed-JSON handling, unified config
  validation, auth-required behavior, health-unhealthy-on-bad-config).
- **Frontend:** `cd web/frontend && npm ci && npm run build && npx tsc --noEmit`.
- **Fresh-clone check:** `dotnet run --project web/src/VoiceLive.Web` builds the bundle with no manual
  npm step (#9 acceptance).
- **Bicep:** `az bicep build -f infra/main.bicep` + `az deployment sub validate` / what-if.
- **Live deploy:** `azd up` against the subscription the user specifies; browser smoke of `/login`,
  `/api/health`, `/api/config`, and a full `/ws/session` avatar session (agent mode).

## 9. Deployment runbook (README "Deploy" section)

1. `az login`; `azd auth login`.
2. `azd env new`; set `AZURE_LOCATION=swedencentral`, `AUTH_USERNAME`, `AUTH_PASSWORD` (azd env or
   prompted). No Entra app registration required.
3. `azd up` — provisions RG + Foundry (account/project/deployment) + App Service + App Insights, runs the
   frontend build (prebuild) and agent creation (postprovision), deploys the app.
4. Browse the App Service URL, log in, run a session.
- Required parameters documented: location, login credentials, optional user-assigned identity client id.

## 10. Risks & open items

| Risk | Mitigation |
|---|---|
| Agent REST path `/assistants` vs `/agents` ambiguous | Verify live against `testlab-f` before trusting; script tries verified path, falls back; else manual step (no fabricated call). |
| `DOTNETCORE|10.0` not offered in region | Self-contained publish fallback; `linuxFxVersion` parameterized. |
| Tenant ALZ policies force `disableLocalAuth`/private KV | We only use managed identity + app settings; `disableLocalAuth:true` is compatible. No KV in v1. |
| App-level password in app settings (not KV) | Documented; fixed-time compare; Key Vault reference is a documented follow-up. |
| Sweden Central Voice Live *agent* support unconfirmed in the region matrix | Verify before deploy; agent mode already works live against `testlab-f` (Sweden Central) per prior evidence. |
| Large PR | Commits grouped by phase/issue; each phase builds and tests green. |

## 11. Issue → change map

| Issue | Where |
|---|---|
| #3 remove /api/token | `Program.cs`, delete `Tokens/`, tests, READMEs |
| #4 auth + origin + session cap + rate limit | `Program.cs`, new `Auth/`, bridge, Bicep app settings |
| #5 bridge hardening | `VoiceLiveWebSocketBridge.cs`, `Program.cs` |
| #2 single credential | `Program.cs` (DI), bridge factory, `VoiceLiveClient` |
| #6 config externalize + shippable | `session.json`, `appsettings.json`, `.csproj` Content, startup load |
| #9 frontend build in publish | `.csproj` target, `.gitignore`/untrack, `package.json` |
| #1 azd + Bicep | `azure.yaml`, `infra/`, `scripts/create-agent.sh` |
| #11 OTel + health | `.csproj` package, `Program.cs`, bridge logging |
| #7 CI | `.github/workflows/ci.yml`, delete `pipeline/` |
| #8 remove CLI | delete `cli/`, docs sweep |
| #10 code quality | unified `WebConfig`, bridge instructions, version validation |
| #12 epic | closed by the PR |
