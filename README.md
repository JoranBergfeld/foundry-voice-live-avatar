# Foundry Voice Live Avatar

A stage-ready conversational avatar built on [Microsoft Foundry Voice Live](https://learn.microsoft.com/azure/ai-foundry/ai-services/). A thin TypeScript browser client pairs with an ASP.NET Core 10 server; the server holds all Azure credentials, owns the Voice Live session lifetime, and bridges audio and control frames. Avatar media flows directly from Azure to the browser over WebRTC.

**Three views**, all served from the same app:

| URL | Purpose |
|-----|---------|
| `/` | Fullscreen avatar landing — audience/presenter display |
| `/?view=operator` | Operator console — session controls, transcript, tool events, diagnostics |
| `/?view=display` | Dedicated display surface for secondary screens |

By default the app runs in **model mode** using `gpt-realtime`. Optional **agent mode** uses a named Voice Live agent created in the Azure AI Foundry portal. Reliability features include manual turn gating (Hold to talk, gated, or open-mic), automatic reconnect, health and error reporting at `/api/health`, safe-question injection, and voice-only fallback when avatar capacity is unavailable.

## How it works

```mermaid
flowchart LR
    subgraph Browser["Authenticated browser"]
        V["Views\n/ · /?view=operator · /?view=display"]
        MIC["Microphone\nPCM16 AudioWorklet"]
        WR["WebRTC peer connection\nreceive-only"]
    end

    subgraph App["ASP.NET Core app"]
        AUTH["Cookie auth\n8-hour sliding"]
        HC["Health + browser-safe config\n/api/health · /api/config"]
        BRIDGE["Voice Live WebSocket bridge\n/ws/session"]
        CFG["Validated config\nconfig/ directory"]
        CRED["DefaultAzureCredential"]
        OT["OpenTelemetry / Azure Monitor"]
    end

    subgraph Azure["Azure"]
        VL["Foundry Voice Live"]
        AS["App Service\n(hosts app, managed identity)"]
        AI["Application Insights"]
    end

    V -->|sign-in cookie| AUTH
    V -->|GET /api/config JSON| HC
    MIC -->|binary PCM16 + JSON controls| BRIDGE
    BRIDGE -->|JSON controls + 24 kHz mono PCM16 over WS| VL
    CRED -->|token via CLI locally\nmanaged identity in Azure| VL
    VL -->|events through app| BRIDGE
    BRIDGE -->|ready · transcripts · avatar-answer · errors| V
    VL -->|direct avatar audio + video| WR
    WR -->|renders in video element| V
    CFG --> BRIDGE
    OT --> AI
    AS -->|RBAC| VL
    AS -.->|hosts ASP.NET Core app| BRIDGE
```

**Two paths after session start:**

1. **Control + audio path** — JSON control frames and binary PCM16 audio travel `browser → /ws/session → Voice Live` and back. The app acts as a trusted relay: it adds the Azure token, enforces the concurrent-session gate, applies config, and sanitizes errors before forwarding events to the browser.
2. **Media path** — avatar audio and video stream directly from Azure to the browser over a receive-only WebRTC peer connection. The app relays only the SDP offer/answer and ICE server list; media bytes never touch the server.

## Quickstart

**Prerequisites**

- .NET 10 SDK
- Node.js 24
- Azure CLI (`az`)
- Access to a Voice Live / avatar-capable Azure AI Foundry resource
- `Cognitive Services User` and `Foundry User` roles on the resource

**Steps**

```bash
az login
# optional: az account set --subscription <subscription-id>
export VoiceLive__Endpoint="https://<your-resource>.services.ai.azure.com"  # Foundry account endpoint
export VoiceLive__Mode=model
dotnet run --project web/src/VoiceLive.Web
```

MSBuild automatically runs `npm ci && npm run build` via the `BuildFrontend` target before the app starts.

Open **http://localhost:5280/** and sign in with the development credentials `operator` / `rehearsal`.

- Grant microphone access when prompted.
- Press and hold **Hold to talk** to speak, or switch turn-taking mode in the operator view.
- Share `/?view=operator` with operators and `/?view=display` with display screens.

**Verify the app is healthy:**

```bash
curl -s http://localhost:5280/api/health; echo
```

`DefaultAzureCredential` picks up your `az login` token locally and the system-assigned managed identity in Azure. If the token expires, restart the session; the credential refreshes automatically between sessions. If avatar capacity is unavailable the session continues in voice-only mode — the avatar video element is hidden and audio keeps working.

## Deploy to Azure

```bash
az login && azd auth login
azd env new <name>
azd env set AZURE_LOCATION swedencentral
azd env set AUTH_USERNAME <username>
azd env set AUTH_PASSWORD <password>
azd up
```

`azd up` provisions a Foundry account and project, a Linux App Service plan (B1), Application Insights, Log Analytics, and RBAC assignments, then builds the frontend and deploys the app. Out of the box it runs in **model mode** (`gpt-realtime`).

**Optional agent mode:** create a Voice Live agent in the Azure AI Foundry portal, set its name in `config/agent.json`, then:

```bash
azd env set VOICELIVE_MODE agent
azd up
```

The `postprovision` hook runs `scripts/setup-agent.sh` (or `.ps1`) which lists existing agents and prints the steps above. See [docs/runbook.md](docs/runbook.md) for environment variables, self-contained deployment, region availability, and day-two operations.

## Session startup

```mermaid
sequenceDiagram
    participant U as User/operator
    participant B as Browser
    participant A as ASP.NET Core app
    participant VL as Foundry Voice Live

    U->>B: Sign in (POST /login)
    B->>A: Authenticated WS upgrade /ws/session
    A->>A: DefaultAzureCredential acquires Azure token
    A->>VL: Start Voice Live session (model or agent mode)
    A->>VL: Configure audio PCM16 24 kHz, noise reduction, turn-taking, avatar
    VL-->>A: ICE servers + session ready
    A-->>B: ready frame with ICE servers
    B->>B: Create receive-only RTCPeerConnection, generate SDP offer
    B->>A: avatar-offer (JSON control)
    A->>VL: Relay SDP offer
    VL-->>A: SDP answer
    A-->>B: avatar-answer frame
    B->>VL: Direct WebRTC media (avatar audio + video)
    U->>B: Hold to talk / safe question
    B->>A: Binary PCM16 or say frame
    A->>VL: Forward audio or say
    VL-->>A: Transcript, speech state, response-done events
    A-->>B: Forward Voice Live events to browser
```

## Architecture

### Application host and endpoints

`Program.cs` is the composition root. It wires:

- **Cookie authentication** — 8-hour sliding session, HttpOnly, SameSite=Lax, Secure outside development.
- **Login rate limiting** — fixed-window 5 attempts / minute per IP; returns 429.
- **HSTS and HTTPS redirect** — enabled outside development.
- **CSP and security headers** — `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`, and a strict `Content-Security-Policy` on every response.
- **WebSocket middleware** — origin validation against `AllowedOrigins` (same-origin allowed by default), 30-second keepalive, concurrent-session gate.
- **Config health** — `ConfigHealthCheck` reports unhealthy if config failed to load at startup.
- **DefaultAzureCredential** — single token credential instance shared across sessions; supports `AZURE_CLIENT_ID` for managed identity client ID.
- **OpenTelemetry + Azure Monitor** — metrics meter `VoiceLive.Web`; Azure Monitor exporter enabled when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set.
- **Static file hosting** — serves the built frontend from `wwwroot`.

**Endpoints:**

| Method | Path | Auth | Responsibility |
|--------|------|------|----------------|
| `GET` | `/login` | anonymous | Login form |
| `POST` | `/login` | anonymous (rate-limited) | Validate credentials, issue cookie |
| `POST` | `/logout` | authenticated | Clear cookie |
| `GET` | `/api/health` | anonymous | Config health check; 200 healthy / 503 unhealthy |
| `GET` | `/api/config` | **required** | Return browser-safe config JSON |
| `WS` | `/ws/session` | **required** | Start server-side Voice Live session; bridge audio and controls |

Unauthenticated HTML requests redirect to `/login`. Unauthenticated `/api/*` and `/ws/*` requests return **401** (no redirect).

### Configuration and session options

```
config/
  agent.json          # agent mode: agent/project name, safe questions, grounding strategy, resume policy
  avatar.json         # avatar character, style, background
  session.json        # model, voice, noise reduction, transcription flags
  session.sample.json # sample session config excluded from publish
  turntaking.json     # mode (gated/open-mic/hybrid) and thresholds
  grounding/          # grounding documents loaded into session context
```

Config is validated at startup by `AppConfigLoader`; the app reports unhealthy and logs a critical error if validation fails. The browser receives only sanitized fields from `/api/config` — Azure endpoints, keys, and internal paths are never exposed.

`SessionOptionsBuilder` constructs the Voice Live session options from validated config:

- **Model or agent mode** — resolved from `VOICELIVE_MODE` / `VoiceLive:Mode`.
- **Audio format** — PCM16, 24 kHz, mono.
- **Processing** — noise reduction, echo cancellation, transcription (configurable).
- **Turn-taking** — gated (Hold to talk), open-mic, or hybrid, with VAD thresholds from `turntaking.json`.
- **Avatar settings** — character, style, background from `avatar.json`.

See [docs/config-schema.md](docs/config-schema.md) for the full schema and all fields.

### Server-side Voice Live bridge

`VoiceLiveWebSocketBridge` manages one session per authenticated browser WebSocket. Two async pumps run concurrently:

**Browser → Voice Live**

| Frame type | Description |
|-----------|-------------|
| Binary | PCM16 audio forwarded as audio input |
| `avatar-offer` | SDP offer relayed to Voice Live |
| `start-turn` / `end-turn` | Manual turn gating |
| `barge-in` | Interrupt current response |
| `say` | Inject text as user turn |
| `ping` | Keepalive (acknowledged) |

**Voice Live → Browser**

| Event | Description |
|-------|-------------|
| `ready` | Session started; includes ICE servers |
| transcript events | Partial and final transcripts |
| speech / avatar state | Speaking, listening, idle states |
| `avatar-answer` | SDP answer from Voice Live |
| tool frames | Tool call notifications |
| `response-done` | Turn complete |
| `avatar-error` | Non-fatal; avatar unavailable, voice continues |
| `error` | Fatal session error |

Inbound browser frames are capped at 1 MiB. Outbound sends are serialized. Active session count, error count, and session duration are tracked as OpenTelemetry metrics. Errors from Voice Live are sanitized before forwarding. The bridge cleans up the Voice Live session and releases the concurrency gate on exit.

### Browser client and views

`views.ts` owns the three view components (landing `/`, operator `/?view=operator`, display `/?view=display`) and their UI state. `main.ts` owns session lifecycle: WebSocket connection, WebRTC setup, AudioWorklet initialization, and event routing.

- **WebRTC** — receive-only `RTCPeerConnection`; audio and video tracks are attached to a `<video>` element when the `avatar-answer` arrives.
- **Audio worklet** — `web/src/VoiceLive.Web/wwwroot/pcm-worklet.js` captures mono microphone input, converts to PCM16, and sends binary frames over the WebSocket.
- **Turn-taking** — gated mode sends `start-turn`/`end-turn` on button press/release; open-mic mode streams continuously; hybrid uses VAD.
- **Transcripts and tools** — displayed in the operator view; tool call events from the bridge are shown as structured notifications.
- **Error and reconnect** — transient errors trigger automatic reconnect with backoff; fatal errors surface in the operator view.
- **Resource teardown** — microphone tracks, AudioContext, WebSocket, and RTCPeerConnection are all closed on session end.

### Authentication and trust boundaries

- **App credentials** (`Auth:Username` / `Auth:Password`) are used only for cookie authentication. Development defaults are `operator` / `rehearsal`.
- **Azure credentials** are managed exclusively server-side via `DefaultAzureCredential`: Azure CLI credentials locally, system-assigned managed identity on App Service.
- The browser **never** receives an Azure token. The `/api/config` endpoint returns only browser-safe fields.
- **RBAC** — the managed identity is assigned `Cognitive Services User` and `Foundry User`. No API keys are used.

### Observability and failure behavior

- **`/api/health`** returns 200 when config is valid and 503 when config failed validation at startup. Use it for App Service health checks and smoke tests.
- **Metrics** — `VoiceLive.Web` meter exposes active session count, session error count, and session duration histograms via OpenTelemetry / Application Insights.

Explicit failure modes:

| Failure | Behavior |
|---------|----------|
| Invalid config at startup | App reports 503 on `/api/health`; bridge sends `error` and closes |
| Login rate limit exceeded | 429 response |
| WebSocket wrong origin | 403 response |
| Concurrency gate full | `error` frame "server is at capacity" |
| Inbound message over 1 MiB | Session closed |
| Voice Live service error | Sanitized `error` frame; session closed |
| Avatar capacity unavailable | `avatar-error` frame; voice-only mode continues |

Automatic reconnect is attempted by the browser for transient disconnects. Each browser tab opens an independent session with its own concurrency slot. Hosted tool calls may not produce a client-side tool event in all configurations.

### Azure deployment architecture

- **Foundry account and project** — local authentication disabled; project-level RBAC only.
- **App Service** — Linux B1 plan, system-assigned managed identity, WebSockets enabled, always-on, TLS 1.2 minimum, health check path `/api/health`.
- **Observability** — Log Analytics workspace, Application Insights connected to the workspace.
- **RBAC** — managed identity assigned `Cognitive Services User` and `Foundry User` on the Foundry project.
- **`azure.yaml`** — `prebuild` hook runs `npm ci && npm run build` in `web/frontend`; `postprovision` hook runs `scripts/setup-agent.sh` to discover existing agents and print agent-mode setup instructions.

## Repository layout

```
config/                   # Runtime configuration (avatar, session, turn-taking, grounding)
docs/                     # Runbook, config schema, rehearsal checklist, specs
infra/                    # Bicep templates for azd (App Service, Foundry, RBAC)
scripts/                  # azd postprovision agent discovery scripts
web/
  frontend/               # TypeScript browser client (main.ts, views.ts, pcm-worklet.js)
    tests/                # Playwright browser tests
  src/VoiceLive.Web/      # ASP.NET Core app (Auth, Config, Health, Session, Program.cs)
  tests/                  # Backend integration and unit tests
azure.yaml                # azd service definition and hooks
README.md
```

## Reference documentation

- [docs/runbook.md](docs/runbook.md) — deployment, environment variables, operations, troubleshooting
- [docs/config-schema.md](docs/config-schema.md) — full config file schema and all fields
- [docs/rehearsal-checklist.md](docs/rehearsal-checklist.md) — pre-show and rehearsal checklist
- [docs/initial-spec.md](docs/initial-spec.md) — original design specification
- [web/README.md](web/README.md) — backend and frontend architecture detail
