# Foundry Voice Live Avatar Runbook

## 1. Overview

This repository contains two operator-facing apps that share the same `/config` directory:

- [`cli/`](../cli/README.md): a voice-only rehearsal harness for validating config and running headless or Windows live-audio checks.
- [`web/`](../web/README.md): the show client, with an ASP.NET Core server-side Voice Live session and browser avatar controls.

The `/config` files hold runtime settings, safe questions, avatar settings, and grounding markdown. See the design background in [`docs/superpowers/specs/2026-07-22-voice-live-avatar-design.md`](superpowers/specs/2026-07-22-voice-live-avatar-design.md).

## 2. Prerequisites

- .NET 10 SDK.
- Node.js for frontend build workflows.
- Azure CLI.
- A signed-in Azure CLI session:

  ```bash
  az login
  az account set --subscription 9bc0bdaa-0a20-4570-9cae-ef826f5c23a7
  ```

- Windows for CLI live microphone/speaker mode. CLI headless `--text` and `--audio-file` modes work on Linux/WSL.

## 3. Provisioning

Region matters. This project pins `swedencentral` because it supports native realtime models (`gpt-realtime`), avatar, and agent mode. West Europe supports avatar but not native realtime models.

The current owner already has a live Azure AI Foundry resource and project that satisfy the verified setup:

- Resource: `testlab-f`
- Kind: `AIServices`
- Region: `swedencentral`
- Resource group: `testlab-foundry`
- Subscription: `9bc0bdaa-0a20-4570-9cae-ef826f5c23a7`
- Account endpoint: `https://testlab-f.services.ai.azure.com`
- Project: `proj-default`
- Project endpoint: `https://testlab-f.services.ai.azure.com/api/projects/proj-default`
- Local auth: disabled (`disableLocalAuth=true`), so API keys are disabled and Microsoft Entra auth is required.

For a new environment, create an Azure AI Foundry/AIServices account in `swedencentral`:

```bash
az cognitiveservices account create \
  --name <account-name> \
  --resource-group <resource-group> \
  --kind AIServices \
  --sku S0 \
  --location swedencentral
```

Create or select the Foundry project in the Azure AI Foundry portal. Do not assume agent or project CLI automation unless it has been live-verified for this repo.

Assign the recommended baseline roles at the account scope:

```bash
az role assignment create \
  --assignee <operator-user-or-group> \
  --role "Cognitive Services User" \
  --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.CognitiveServices/accounts/<account-name>

az role assignment create \
  --assignee <operator-user-or-group> \
  --role "Foundry User" \
  --scope /subscriptions/<subscription-id>/resourceGroups/<resource-group>/providers/Microsoft.CognitiveServices/accounts/<account-name>
```

## 4. RBAC and authentication

Both apps use `DefaultAzureCredential`; operators authenticate with `az login`. No API keys or app secrets are used. The web token broker requests the scope `https://ai.azure.com/.default`.

Recommended baseline RBAC is:

- `Cognitive Services User`
- `Foundry User`

In live testing, the signed-in user having `Foundry User` on the account was sufficient for Voice Live model mode and avatar media. Treat the recommended pair above as the baseline; do not over-interpret the test as proving a single minimal role for every environment.

## 5. Configuration

Full field reference: [`docs/config-schema.md`](config-schema.md).

Before rehearsal, verify the operator-owned values in `/config`:

- `config/session.json`: `endpoint` (`https://testlab-f.services.ai.azure.com` for the current resource), `region` (`swedencentral`), `apiVersion` (`2025-10-01`), `model` (`gpt-realtime`), voice, and input audio settings.
- `config/agent.json`: `agentName` (`company-direction-avatar`), `agentProjectName` (`proj-default`), resume policy, grounding strategy, and safe questions.
- `config/avatar.json`: avatar character/style (`lisa`, `casual-sitting` in the verified setup).
- `config/grounding/company-direction.md`: event-ready grounding content.

Both apps validate config at startup.

## 6. Running model mode

Model mode is fully live-verified and requires no model deployment. The bare model name `gpt-realtime` resolves server-side.

Validate the shared config:

```bash
dotnet run --project cli/src/VoiceLive.Cli -- validate --config config
```

Run a headless model-mode smoke test:

```bash
dotnet run --project cli/src/VoiceLive.Cli -c Release -- run --config config --mode model --text "Say hello in one short sentence." --seconds 30
```

On Windows, run live microphone/speaker mode by omitting `--text` and `--audio-file`:

```bash
dotnet run --project cli/src/VoiceLive.Cli -c Release -- run --config config
```

Run the web show client from the repository root:

```bash
ConfigDir=/home/jbergfeld/vcs/foundry-voice-live-avatar/config ASPNETCORE_URLS=http://127.0.0.1:5210 dotnet run --no-launch-profile --project web/src/VoiceLive.Web
```

Open:

- Operator view: `http://127.0.0.1:5210/?view=operator`
- Display view: `http://127.0.0.1:5210/?view=display`

Quick checks:

```bash
curl -s http://127.0.0.1:5210/api/health
curl -s http://127.0.0.1:5210/api/config
```

Known MVP limitation: each browser tab opens its own `/ws/session`, which creates its own server-side session. The operator tab is the complete self-contained experience. A shared operator+display room is future work.

## 7. Avatar operation

Avatar is fully live-verified with character `lisa` and style `casual-sitting`. Media flows browser ↔ Azure over WebRTC; the server relays SDP and ICE. A headless browser E2E reached WebRTC `connected` state with video and audio tracks arriving, and the safe-question path produced streaming transcripts plus a completed response.

Real browsers require a user gesture before video/audio autoplay. On the event machine, the operator must interact with the page: grant microphone permission, then hold to talk or click a safe question. If the browser blocks autoplay, the UI shows a clear banner asking the operator to interact with the page.

## 8. Agent mode

Agent mode is blocked until the Foundry agent exists.

- Agent mode plumbing compiles against the real SDK but is **NOT YET LIVE-VERIFIED**.
- Project `proj-default` currently has zero agents.
- Agent mode cannot run until an agent named `company-direction-avatar` exists in `proj-default`.
- Creation is expected via the future `tools/sync-agent` workflow or the Azure AI Foundry portal. This is **NOT YET LIVE-VERIFIED** for this repo; do not present it as an event-ready path.

## 9. Failure handling

Failures are explicit and visible, not masked:

- The server forwards service errors to the browser as an `error` frame and closes the session.
- The browser shows an error banner.
- The token broker returns HTTP 502 with a clear message when Azure auth fails; it never returns a fake token.

## 10. Troubleshooting

| Symptom | Likely cause | Operator action |
| --- | --- | --- |
| `/api/token` or session startup reports auth failure | `az login` expired, wrong tenant/subscription, or missing RBAC | Run `az login`, `az account set --subscription 9bc0bdaa-0a20-4570-9cae-ef826f5c23a7`, and confirm the recommended account-scope roles. |
| No avatar video/audio in browser | Autoplay blocked, mic permission not granted, or WebRTC setup did not complete | Grant mic permission, click/press a control in the operator page, and reload the tab if the session closed. |
| Model mode unavailable or realtime model not found | Resource in a region without native realtime model support | Use `swedencentral`; West Europe is not sufficient for native `gpt-realtime`. |
| Agent mode unavailable | `company-direction-avatar` does not exist in `proj-default` | Use model mode for rehearsal/show until the agent is created via the portal or future `tools/sync-agent` workflow. |
