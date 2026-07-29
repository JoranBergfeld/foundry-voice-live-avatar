# README Documentation Update Design

**Date:** 2026-07-29  
**Status:** Approved  
**Primary audience:** Developers evaluating, running, or extending the application

## Goal

Rewrite the repository README so a developer can quickly understand what the application does, see how its runtime pieces interact, run it locally, and understand the detailed architecture without first navigating several supporting documents.

The README will remain the primary orientation page. Operational procedures, exhaustive configuration fields, and rehearsal guidance will continue to live in their existing reference documents.

## README structure

The README will use this order:

1. Product description and primary use case.
2. User-facing views and capabilities.
3. Mermaid system overview.
4. Local-first quickstart.
5. Azure deployment summary.
6. Detailed architecture.
7. Repository layout and reference documentation links.

The current deployment and security material will be retained where useful, but reorganized around this developer-first flow.

## Product description

The opening will describe the application as a stage-ready conversational avatar built on Microsoft Foundry Voice Live. It will explain that:

- the browser captures microphone audio and renders the avatar;
- the ASP.NET Core server owns Azure credentials, validates configuration, and hosts the Voice Live session;
- operators can use a minimal landing view, a detailed operator view, or a passive display view;
- the application supports direct model sessions and optional Foundry Voice Live agent sessions;
- reliability features include explicit session status, manual turn gating, safe questions, reconnect controls, and voice-only degradation when avatar capacity is unavailable.

## Visual overview

The README will include Mermaid diagrams rather than external images.

### System context diagram

The first diagram will show:

- the authenticated browser;
- the ASP.NET Core application;
- the Voice Live service;
- repository runtime configuration;
- App Service managed identity;
- Application Insights;
- the two media paths.

The diagram must visually separate:

- the WebSocket audio and control plane, which passes through `/ws/session` on the server; and
- the avatar WebRTC plane, which connects Voice Live directly to the browser after the server relays SDP and ICE metadata.

### Session startup sequence

The second diagram will show:

1. Cookie authentication.
2. Browser WebSocket connection.
3. Server authentication to Azure with `DefaultAzureCredential`.
4. Voice Live session creation and configuration.
5. The server's `ready` frame with browser-safe config and ICE servers.
6. WebRTC offer and answer relay.
7. Browser microphone capture and PCM16 streaming.
8. Transcript, status, and avatar response events.

## Quickstart

The quickstart will optimize for local development.

### Prerequisites

- .NET 10 SDK.
- Node.js 24.
- Azure CLI.
- Access to a Voice Live-capable Azure AI Foundry resource.
- An authenticated Azure CLI session with the required resource roles.

### Steps

The README will guide the developer to:

1. Run `az login` and select the appropriate subscription if necessary.
2. Start the application from the repository root with:

   ```bash
   dotnet run --project web/src/VoiceLive.Web
   ```

3. Open `http://localhost:5280/`.
4. Sign in with the development credentials `operator` / `rehearsal`.
5. Grant microphone access and use the configured talk control.
6. Optionally open `?view=operator` for diagnostics or `?view=display` for the passive view.
7. Check `http://localhost:5280/api/health` when diagnosing startup configuration.

The quickstart will note that frontend assets are built by the MSBuild target and that Azure authentication failures require `az login` or corrected role assignments. Avatar capacity errors will be described as a voice-only degradation rather than a complete session failure.

Azure deployment will remain a separate concise section below the local quickstart and link to the runbook for full operational detail.

## Detailed architecture

### Application host

`web/src/VoiceLive.Web/Program.cs` is the composition root. The architecture description will cover:

- cookie authentication and login rate limiting;
- security headers, HTTPS/HSTS behavior, and WebSocket origin validation;
- static frontend hosting;
- `/api/health`, `/api/config`, login/logout, and `/ws/session`;
- startup configuration validation;
- concurrent-session limiting;
- `DefaultAzureCredential`;
- OpenTelemetry metrics and optional Azure Monitor export.

### Configuration

The README will explain that configuration is loaded and validated on the server from `/config` plus application settings. The browser receives only sanitized fields through `/api/config` and the WebSocket `ready` frame. The server builds Voice Live SDK options for:

- model or agent mode;
- voice and audio formats;
- turn detection and manual turn gating;
- noise reduction and echo cancellation;
- transcription;
- avatar character, style, video, and background;
- model grounding instructions.

The exhaustive field reference remains in `docs/config-schema.md`.

### Voice Live bridge

`VoiceLiveWebSocketBridge` owns one Voice Live session per browser WebSocket. The README will describe its two concurrent pumps:

- browser-to-service messages: PCM16 audio plus JSON controls such as avatar offer, turn start/end, barge-in, safe-question prompts, and ping;
- service-to-browser events: readiness, ICE metadata, transcripts, speech/avatar state, SDP answer, tool activity, response completion, and errors.

It will mention the inbound message-size limit, serialized WebSocket sends, keepalive behavior, safe error messages, metrics, and deterministic session cleanup.

### Browser client

The TypeScript client will be described as a thin transport and presentation layer that:

- selects the landing, operator, or display view;
- opens `/ws/session`;
- negotiates a receive-only WebRTC avatar connection;
- captures microphone audio through an `AudioWorklet`;
- sends 24 kHz mono PCM16 audio;
- implements gated, open-mic, and hybrid controls;
- renders transcripts, status, tool activity, errors, and reconnect controls;
- tears down microphone, audio, WebRTC, and WebSocket resources on disconnect.

### Runtime flows

The architecture section will explicitly distinguish:

- **Audio and control:** browser to ASP.NET Core over WebSocket, then ASP.NET Core to Voice Live through the .NET SDK.
- **Avatar media:** Voice Live to browser over direct WebRTC after SDP and ICE relay.
- **Credentials:** browser cookie for app access; Azure CLI credentials locally or App Service managed identity in Azure.
- **Modes:** model mode supplies model and instructions from application configuration; agent mode delegates model, instructions, and hosted tools to the configured Foundry agent.

### Deployment infrastructure

The README will summarize the Bicep-provisioned resources:

- Azure AI Foundry account and project;
- Linux App Service with system-assigned managed identity and WebSockets;
- App Service plan;
- Log Analytics and Application Insights;
- RBAC assignments for Voice Live access.

The browser never receives an Azure access token or Voice Live endpoint credential.

### Failure behavior and limitations

The architecture description will document:

- invalid startup configuration makes health checks fail;
- unauthenticated APIs and WebSockets return 401;
- session capacity and oversized messages are rejected explicitly;
- fatal Voice Live failures close the session with a user-visible error;
- avatar capacity errors disable WebRTC avatar rendering while the voice session continues;
- each browser tab currently creates an independent Voice Live session;
- hosted agent tools may execute without producing a discrete client-visible tool event.

## Documentation boundaries

The README will link to, rather than duplicate:

- `docs/runbook.md` for deployment, operation, and troubleshooting;
- `docs/config-schema.md` for exhaustive configuration fields and validation rules;
- `docs/rehearsal-checklist.md` for event preparation;
- existing design specs for historical design decisions.

## Validation

The completed README update must be checked for:

- accurate commands, URLs, credentials, component names, and runtime behavior;
- valid Mermaid syntax and readable diagram labels;
- working relative links;
- consistency with the current ASP.NET Core, TypeScript, Bicep, and `azd` implementation;
- no duplication that could make the README conflict with the runbook or config schema.
