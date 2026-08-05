# Foundry Voice Live Avatar Runbook

> **Scope:** provisioning and **rehearsal**. For identity, secrets, capacity, cost, alerting, rollback and disaster recovery, see [`production-deployment.md`](production-deployment.md).

## 1. Overview

This repository contains one operator-facing ASP.NET Core web app at [`web/`](../web/README.md). The app ships the `/config` directory, authenticates operators with app-level cookie auth, hosts the Voice Live session server-side, and bridges browser audio/control messages over `/ws/session`.

The `/config` files hold runtime voice/audio/avatar settings, safe questions, agent metadata, and grounding markdown. Endpoint, API version, and mode are app settings under `VoiceLive:*`. Design background (historical, not maintained): [`history/initial-spec.md`](history/initial-spec.md).

## 2. Prerequisites

- .NET 10 SDK for local runs.
- Node.js for frontend build workflows.
- Azure CLI and Azure Developer CLI (`azd`) for deployment.
- A signed-in Azure CLI session for local Voice Live access:

  ```bash
  az login
  az account set --subscription <subscription-id>
  ```

No Microsoft Entra app registration is required. In Azure, the App Service uses its system-assigned managed identity.

## 3. Provisioning and deployment

Region matters. This project pins `swedencentral` because it supports native realtime models (`gpt-realtime`), avatar, and agent mode. West Europe supports avatar but not native realtime models.

Use `azd` for new environments:

```bash
az login && azd auth login
azd env new <name>
azd env set AZURE_LOCATION swedencentral
azd env set AUTH_USERNAME <user>
azd env set AUTH_PASSWORD <password>
azd up
```

`azd up` provisions, via `infra/main.bicep` and `infra/resources.bicep`:

- an Azure AI Foundry account (`Microsoft.CognitiveServices/accounts`, kind `AIServices`) and project (`proj-default`), with local auth disabled;
- a Linux App Service with system-assigned managed identity, WebSockets enabled, health-check path `/api/health`, and always-on;
- Log Analytics and workspace-based Application Insights;
- RBAC role assignments granting the app managed identity `Cognitive Services User` and `Foundry User` on the account/project.

The `azure.yaml` prebuild hook runs `npm ci && npm run build` in `web/frontend`. The postprovision hook (`scripts/setup-agent.sh` / `.ps1`) detects existing Voice Live agents and prints how to enable agent mode; it does not create an agent.

If `DOTNETCORE|10.0` is unavailable in the target App Service region, run:

```bash
azd env set LINUX_FX_VERSION ""
azd up
```

That clears the platform runtime so deployment can use the app's self-contained publish output.

## 4. RBAC and authentication

The web app uses two authentication layers:

- Operators sign in to the app with ASP.NET Core cookie auth. Credentials come from `Auth:Username` / `Auth:Password` (`Auth__Username` / `Auth__Password` in environment variables or App Service app settings).
- The server talks to Voice Live with `DefaultAzureCredential`: Azure CLI credentials for local runs, and the App Service managed identity in Azure.

The browser never receives an Azure token or Voice Live credential. Recommended baseline RBAC for the identity that calls Voice Live is:

- `Cognitive Services User` (`a97b65f3-24c7-4388-baec-2e87135dc908`)
- `Foundry User` (`53ca6127-db72-4b80-b1b0-d745d6d5456d`) on the Foundry account/project.
  - **Former name:** `Foundry User` was previously displayed as *Azure AI User* by the Azure portal — it is the same role definition; assign by GUID if the names are confusing.

## 5. Configuration

Full field reference: [`docs/config-schema.md`](config-schema.md).

Before rehearsal, verify the operator-owned values in `/config` and app settings:

- App settings: `VoiceLive:Endpoint` (`VoiceLive__Endpoint`), `VoiceLive:ApiVersion` (`VoiceLive__ApiVersion`), and `VoiceLive:Mode` (`VoiceLive__Mode` or `VOICELIVE_MODE`).
- `config/session.json`: region, model (`gpt-realtime` for model mode), voice, and input audio settings. `session.model` is required only in model mode.
- `config/agent.json`: `agentName`, `agentProjectName`, and safe questions.
- `config/avatar.json`: avatar character/style.
- `config/grounding/company-direction.md`: event-ready grounding content.

The app validates config at startup. `GET /api/health` returns 200 when config is valid and 503 when config failed to load.

## 6. Running model mode

Model mode is the default and requires no model deployment. The bare model name `gpt-realtime` resolves server-side.

Run the web app locally from the repository root:

```bash
dotnet run --project web/src/VoiceLive.Web
```

Open one or two views — do **not** open all three simultaneously; the default session cap is **2 concurrent sessions** (`MaxConcurrentSessions`):

- Operator/troubleshoot view: `http://localhost:5280/?view=operator`
- Display view (passive): `http://localhost:5280/?view=display`

(Landing `/` also starts a session; if you open it, close it before opening a second view.)

Sign in with the credentials you configured via `dotnet user-secrets` (see [CONTRIBUTING.md](../CONTRIBUTING.md)), grant microphone permission, then hold **Hold to talk** or click a safe question.

Quick anonymous health check:

```bash
curl -s http://localhost:5280/api/health
```

For deployed environments, use `azd up`, open the printed URL, and sign in with the configured `AUTH_USERNAME` / `AUTH_PASSWORD`.

Known MVP limitation: each browser tab opens its own `/ws/session`, which creates its own server-side session. The display tab therefore runs an independent Voice Live session whose avatar does **not** mirror the operator's conversation and produces **no** audio — room audio must come from the operator machine. The operator tab is the complete self-contained experience; a shared operator+display room with one conversation across two screens is future work.

## 7. Avatar operation

Media flows browser ↔ Azure over WebRTC; the server relays SDP and ICE.

`npm --prefix web/frontend test` is the automated **frontend regression suite**: it runs headless against a static file server (`python3 -m http.server`) with no backend — WebSocket, RTCPeerConnection, `getUserMedia`, and `AudioContext` are replaced by test mocks. It verifies UI behaviour and lifecycle logic; it does not confirm the Azure avatar path.

The true **end-to-end check is manual**: start the app, open the operator view, watch the WebRTC status indicator reach **connected**, then ask a safe question and confirm video and audio arrive and the transcript completes.

Real browsers require a user gesture before video/audio autoplay, and the avatar stream carries audio. On the event machine the operator must interact with the page **before** the avatar stream arrives: sign in, grant microphone permission, then hold to talk or click a safe question.

**If the browser blocks autoplay, the session ends.** The app calls `play()` on an unmuted element; any rejection other than `AbortError` — including the `NotAllowedError` browsers raise for blocked autoplay — tears down the whole Voice Live session, not just the video. The UI then shows a fatal error banner reading "Browser blocked avatar playback…". Recovery: click the **Reconnect** button (the click itself satisfies the browser gesture requirement and restarts the session); reload the tab only if reconnection still fails.

This is most dangerous on `/?view=display`, which is designed to run unattended and offers no interaction affordance of its own. **Always click into a display screen once, before the audience is present.** Tracked as finding H-05 in [`review-merged.md`](../review-merged.md); once fixed, blocked autoplay will retry muted instead of ending the session and this section must be updated.

## 8. Agent mode

Agent mode is optional. It requires a Voice Live agent created in the Azure AI Foundry portal.

To enable agent mode:

1. Create a Voice Live agent in the Azure AI Foundry portal (`https://ai.azure.com`).
2. Set the agent name and project in `config/agent.json`.
3. Run `azd env set VOICELIVE_MODE agent`.
4. Run `azd up` again.

The postprovision hook detects and lists existing agents and prints these opt-in steps. It does not create or modify agents. In agent mode the agent owns the model, instructions, and hosted tools; voice, avatar, audio, and turn-taking still come from app config.

## 9. Failure handling

Failures are explicit and visible, not masked:

- Config load failures make `/api/health` return 503.
- Unauthenticated HTML requests redirect to `/login`.
- Unauthenticated `/api/*` and `/ws/*` requests return 401.
- The server forwards fatal service errors to the browser as an `error` frame and closes the session.
- Avatar rendering capacity/quota errors are non-fatal at the session level: the server sends an `avatar-error` frame. **However, avatar audio is lost along with the video**, because both arrive over the same WebRTC peer connection and the server does not forward audio to the browser over the WebSocket. The WebSocket connection, microphone capture, and transcripts survive, but there is no audible output to the room. Operators must invoke a fallback plan (hand off to a human presenter or restart against an avatar-capable resource). This is a known gap, not a design decision; forwarding `response.audio.delta` to the browser and playing it would make true voice-only fallback possible, at which point this section must be updated.
- The browser shows an error banner for fatal errors, or a non-fatal "Avatar unavailable" notice when the avatar connection fails; in either case the operator must act (see troubleshooting below).
- On any fatal disconnect the browser shows a **Reconnect** button. Clicking it re-runs the full connection flow (`start()`), restoring the session without a tab reload — preserving the sign-in cookie, microphone grant, and any autoplay gesture already obtained. Use Reconnect as the first recovery action for a closed session; reload only if Reconnect fails.

## 10. Troubleshooting

| Symptom | Likely cause | Operator action |
| --- | --- | --- |
| Sign-in fails or session reports auth failure | Wrong app credentials, `az login` expired locally, wrong tenant/subscription, or missing RBAC | Check `Auth__Username` / `Auth__Password` in App Service settings; for local dev run `az login`, `az account set --subscription <subscription-id>`, and confirm the server identity has the recommended roles. |
| `/api/health` returns 503 | Config failed to load or required app settings are missing | Check App Service settings for `VoiceLive__Endpoint`, `VoiceLive__ApiVersion`, `VoiceLive__Mode`, and auth settings; review Application Insights logs. |
| No avatar video/audio in browser | Autoplay blocked, mic permission not granted, or WebRTC setup did not complete | Grant mic permission, click/press a control in the operator page. If the session closed, click **Reconnect** first (a reload is only needed if Reconnect fails). |
| Avatar never appears; log shows `avatar_service_resource_exhausted` | The Voice Live resource has little or no avatar rendering quota (common on a freshly provisioned resource) | Request an avatar rendering quota increase for the resource via Azure support, or set `VoiceLive__Endpoint` to an avatar-enabled resource. **Note:** avatar audio is also lost when the avatar is unavailable (both ride the same WebRTC peer connection), so there is no voice-only fallback — invoke your fallback plan. |
| Model mode unavailable or realtime model not found | Resource in a region without native realtime model support | Use `swedencentral`; West Europe is not sufficient for native `gpt-realtime`. |
| Agent mode unavailable | The configured Voice Live agent does not exist in the configured Foundry project | Use model mode, or create the Voice Live agent in the Azure AI Foundry portal and redeploy with `VOICELIVE_MODE=agent`. |
| App Service deployment fails because `.NET 10` is unavailable | `DOTNETCORE\|10.0` is not available in the selected region | Run `azd env set LINUX_FX_VERSION ""` and redeploy self-contained. |
