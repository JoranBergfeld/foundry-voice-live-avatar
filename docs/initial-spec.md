# Voice Live Avatar — Project Specification

> **Status: historical.** This is the original design specification, retained for context. It records intent at the time of writing and is **not** maintained against the current implementation. For behaviour that is warranted accurate, see the [project README](../README.md). The use case and design rationale in §1 have been promoted to the [project README](../README.md#why-this-exists).
>
> **Status at time of writing:** Current architecture after App Service managed-identity consolidation

**Owner:** Joran
**Primary audience of this document:** Coding agent and human reviewers
**Last updated:** 2026-07-23

---

## 1. Context and goal

We are building a single ASP.NET Core web app for a conversational avatar on **Microsoft Foundry's Voice Live API**. The app runs the on-stage operator experience, serves the browser avatar UI, authenticates operators with app-level cookie auth, and holds the Azure credential on the server.

**Use case:** the avatar converses **on stage with a C-level leader**, explaining the direction of the company, in front of a live audience. This may happen in a **noisy environment**.

Consequences of that use case, which shape every decision below:

- **Reliability and rehearsability beat features.** Anything that can fail mid-show needs a defined behavior and an operator control.
- The browser must never receive an Azure token or Voice Live credential.
- The app must deploy cleanly to Azure App Service with `azd` and a system-assigned managed identity.

## 2. Platform facts

- Voice Live is accessed over a **WebSocket** carrying JSON events (session config, audio in/out, turn detection, tool calls). The server uses the **`Azure.AI.VoiceLive`** .NET SDK.
- **Avatar video is a separate plane**: it is streamed over **WebRTC**, negotiated by sending a `session.avatar.connect` event containing the client's SDP offer. Video terminates in the browser.
- Session options relevant to us: Azure **semantic VAD**, **end-of-utterance detection**, **deep noise suppression**, **echo cancellation**, avatar character/resolution/bitrate config, OpenAI/Azure voices, and **agent mode**.

## 3. Architecture

### 3.1 Web app

**Stack:** ASP.NET Core backend + thin TypeScript frontend. The app lives at `web/src/VoiceLive.Web`; frontend assets are built into the web app.

**Data flow:**

- **Audio + control plane:** Browser → `/ws/session` → ASP.NET Core backend → Voice Live API. The backend holds credentials, creates the session, applies app/config settings, and proxies events.
- **Video plane:** Browser ↔ Voice Live over a **direct WebRTC connection**. The backend relays the SDP offer/answer; the browser's `RTCPeerConnection` attaches the avatar stream to a `<video>` element.

**HTTP/WS endpoints:**

- `GET /api/health` — anonymous health check; 200 when config is valid, 503 when config failed to load.
- `GET /login`, `POST /login`, `POST /logout` — app-level cookie auth.
- `GET /api/config` — authenticated browser-safe config.
- `WS /ws/session` — authenticated server-side Voice Live bridge.

**Two views:**

| | Display view | Operator view |
|---|---|---|
| Audience-facing | Yes | No |
| Content | Fullscreen avatar video only. | Device/mic controls, hold-to-talk, safe questions, session status, and panic controls. |
| Failure behavior | Avoid going black; operator can reload/restart the session. | Show alert and recovery controls. |

## 4. Authentication, identity, and deployment

Operators authenticate to the app with ASP.NET Core cookie auth. Credentials come from `Auth:Username` / `Auth:Password` (`Auth__Username` / `Auth__Password` in environment variables or App Service app settings). Local development credentials live in `web/src/VoiceLive.Web/appsettings.Development.json`.

The server talks to Voice Live with `DefaultAzureCredential`: Azure CLI credentials locally and the App Service system-assigned managed identity in Azure. No Microsoft Entra app registration is required.

`azd up` provisions the Azure AI Foundry account and project, Linux App Service, Application Insights, Log Analytics, and RBAC role assignments. The App Service has WebSockets enabled, `/api/health` as its health-check path, and always-on.

## 5. Modes, grounding, and agents

**Model mode is the default.** It uses `gpt-realtime`, which Voice Live resolves server-side with no model deployment. Grounding markdown from `/config/grounding/` is used directly as the model's system instructions.

**Agent mode is opt-in.** Create a Voice Live agent in the Azure AI Foundry portal, set `agentName` and `agentProjectName` in `config/agent.json`, set `VOICELIVE_MODE=agent`, and redeploy. In agent mode the agent owns instructions, model choice, and hosted tools.

## 6. Configuration

The `config/` directory is shipped with the web app:

```
/config/
  avatar.json        # character, customized flag, resolution, bitrate
  session.json       # model-mode model, voice, audio, transcription, region
  turntaking.json    # active mode + per-mode parameters
  agent.json         # agent name/project, resume policy, safe-question prompts
  grounding/         # versioned grounding-pack content (markdown)
```

`endpoint`, `apiVersion`, and `mode` are app settings under `VoiceLive:*`, not `session.json` fields. See [`docs/config-schema.md`](config-schema.md).

## 7. Turn-taking policy

A named mode in config controls turn-taking:

| Mode | Turn start | Turn end | Barge-in | Intended use |
|---|---|---|---|---|
| `open-mic` | Semantic VAD | End-of-utterance detection | Enabled | Rehearsal experiment; natural feel |
| `gated` | Mic gate opened (key/click) | Mic gate closed | Disabled — avatar always finishes | **Stage default.** Bulletproof in noise. |
| `hybrid` | Mic gate opened | Semantic VAD / EOU | Only while gate is open | Middle ground |

All modes enable deep noise suppression and echo cancellation in session config.

## 8. Repository layout

```
/web/        # ASP.NET Core backend + TS frontend
/config/     # runtime configuration shipped with the app
/docs/       # config schema, rehearsal checklist, runbook, specs
/infra/      # azd/Bicep infrastructure
/scripts/    # deployment helper scripts
README.md    # orientation and quick start
```

## 9. Show-hardening requirements

1. **Pre-warm:** operator connects and verifies avatar video before the show.
2. **Panic controls:** stop-speaking, repeat-last-answer, safe-question.
3. **Network:** wired ethernet or dedicated hotspot; venue Wi-Fi is a documented non-option.
4. **Security:** WebSocket Origin validation, concurrent-session cap, inbound message-size cap, ping/pong keepalive, and Production HSTS/HTTPS/security headers.
5. **Fallback asset:** a pre-recorded video of the avatar answering the three most likely questions lives on the operator machine; the runbook defines when to cut to it.

## 10. Open items

- Final turn-taking mode and thresholds.
- Grounding pack versus portal-authored agent instructions for the event path.
- Voice selection and style.
- Whether `hybrid` mode earns its keep or gets deleted.
