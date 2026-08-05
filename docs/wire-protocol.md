# Wire protocol reference

**Authoritative reference for `/ws/session`.** If another document contradicts this one, this one is correct — and the other document is a bug. Do not restate frame vocabulary elsewhere; link here.

Verified against `web/frontend/src/main.ts`, `web/frontend/src/views.ts`, `web/src/VoiceLive.Web/Session/VoiceLiveWebSocketBridge.cs`, and `web/src/VoiceLive.Web/Program.cs` at commit `d657e86` (HEAD of the `docs-alignment` branch).

**Note on the discriminator field.** Every JSON frame — in both directions — uses `"t"` (not `"type"`) as its type discriminator. The client parses `frame.t`; the server switches on `tProp.GetString()` from property `"t"`. Any other documentation that refers to a `"type"` field in a frame shape is incorrect.

## Endpoints

| Method | Path | Auth | Purpose |
|---|---|---|---|
| `GET` | `/` | Cookie | Application shell. `?view=operator`, `?view=display`, or the default landing view. |
| `GET` | `/login` | Anonymous | Sign-in form. |
| `POST` | `/login` | Anonymous | Credential submission; issues the auth cookie. |
| `POST` | `/logout` | Anonymous | Clears the auth cookie. Deliberately exempted from the authentication middleware (see `Program.cs:95-97`); note finding H-02. |
| `GET` | `/api/health` | Anonymous | Health and configuration-validity report. Returns 200 when config is valid; 503 when config failed to load. |
| `GET` | `/api/config` | Cookie | Browser-safe config JSON (region, API version, model, voice, avatar settings, turn-taking mode, agent metadata, safe questions). See [`ClientConfig`](#clientconfig) below. |
| `GET` | `/ws/session` | Cookie | WebSocket upgrade. One connection = one Voice Live session = one concurrency slot. |

## Connection lifecycle

1. Browser opens the WebSocket to `/ws/session` with the auth cookie.
2. Server validates the cookie and the `Origin` header, then acquires a slot from the concurrency gate (`MaxConcurrentSessions`, default **2**, bound from ASP.NET configuration via `VoiceLiveOptions`). Rejection closes the socket.
3. Server acquires an Azure token via `DefaultAzureCredential`, builds session options and connects upstream to Voice Live. **The browser never receives an Azure token.**
4. Server sends `ready`. The client must wait for `ready` before sending anything else.
5. Immediately on receiving `ready`, the browser initiates WebRTC negotiation: it creates an `RTCPeerConnection`, adds `recvonly` video and audio transceivers, creates an SDP offer, waits for ICE gathering to complete (or times out after 2.5 s), and sends `avatar-offer`. The server relays the SDP to Voice Live and replies with `avatar-answer`. Avatar media then flows **directly** between browser and Azure over WebRTC — the server never forwards audio to the browser over the WebSocket. See [ADR 0002](adr/0002-direct-webrtc-media-plane.md).
6. **Interactive views only** (landing, operator): after WebRTC negotiation, the client requests microphone permission, initialises the AudioWorklet, and begins turn handling. The **display view** (`?view=display`) opens the WebSocket, receives `ready`, and completes WebRTC negotiation exactly like other views, but `prepareMicrophone` and `wireInteractiveControls` return immediately for non-interactive views — the display view can never send turn frames (`start-turn`, `end-turn`, `barge-in`, `say`).
7. Turns proceed (below). Audio uplink is raw binary (PCM16 mono 24 kHz); everything else is JSON text.
8. Either side closing releases the concurrency slot.

## Browser → server

| Frame | Payload | When |
|---|---|---|
| *(binary)* | PCM16 mono 24 kHz audio | Continuously while microphone streaming is active. Raw binary frames — not JSON. **Interactive views only.** |
| `avatar-offer` | `{ "t": "avatar-offer", "sdp": string }` | Unconditionally, once, immediately after `ready`, by all views including display. |
| `start-turn` | `{ "t": "start-turn" }` | Operator presses **Hold to talk** (gated mode). **Interactive views only.** |
| `end-turn` | `{ "t": "end-turn" }` | Operator releases **Hold to talk**. **Interactive views only.** |
| `barge-in` | `{ "t": "barge-in" }` | Operator presses **Stop speaking** to interrupt avatar speech. **Operator view only.** |
| `say` | `{ "t": "say", "text": string }` | Safe-question injection (one-click buttons or **Repeat**). **Unconstrained today** — see finding H-01. **Operator view only.** |
| `ping` | `{ "t": "ping" }` | Keepalive, sent every 25 s. Answered with `pong`. |

## Server → browser

All are JSON text frames with a `t` discriminator.

| Frame | Payload | Meaning |
|---|---|---|
| `ready` | `{ "t": "ready", "config": ReadyConfig, "iceServers": IceServer[] }` | Session established. Always the first frame. |
| `user-transcript` | `{ "t": "user-transcript", "text": string, "final": boolean }` | Speech-to-text of the operator. **Only emitted in `open-mic` and `hybrid` modes** — gated mode uses `NoTurnDetection` and `InputAudioTranscription` is never set (`SessionOptionsBuilder.cs:35-41`), so no transcript events fire. In open-mic/hybrid: `final: false` frames carry a delta that must be **appended** to the live transcript line; the `final: true` frame carries the complete transcript and **replaces** the accumulated text. |
| `agent-transcript` | `{ "t": "agent-transcript", "text": string, "final": boolean }` | The avatar's response text; emitted from both audio-transcript and text-delta update paths. `final: false` frames carry a delta to append; `final: true` carries the complete text and replaces the accumulated line. |
| `speech-started` | `{ "t": "speech-started" }` | Server-side VAD detected speech. **Only emitted in `open-mic` and `hybrid` modes** (gated uses `NoTurnDetection`). |
| `speech-stopped` | `{ "t": "speech-stopped" }` | Server-side VAD detected end of speech. **Only emitted in `open-mic` and `hybrid` modes** (gated uses `NoTurnDetection`). |
| `avatar-speaking` | `{ "t": "avatar-speaking" }` | Avatar audio playback began. |
| `avatar-idle` | `{ "t": "avatar-idle" }` | Avatar finished speaking. |
| `avatar-answer` | `{ "t": "avatar-answer", "sdp": string }` | WebRTC answer (SDP string decoded from the base64-wrapped JSON the service returns). The browser applies it as the remote description. |
| `response-done` | `{ "t": "response-done" }` | The turn's response is complete. |
| `tool` | `{ "t": "tool", "phase": string, "name": string \| null, "callId": string \| null }` | Tool invocation progress. `phase` values include `"args"`, `"done"`, `"list"`, `"list-done"`, `"list-failed"`. `name` is non-null only for phase `"done"`; `callId` is always present. Hosted tools may emit no client event at all. |
| `avatar-error` | `{ "t": "avatar-error", "code"?: string, "message": string }` | **Non-fatal to the WebSocket session.** Avatar capacity or quota exhausted. The browser's `handleAvatarError` closes the `RTCPeerConnection`; both avatar video **and audio** are lost (both transceivers ride the same peer connection). The WebSocket, microphone capture, and transcripts survive, but there is no audible output to the room. **There is no voice-only fallback.** Operators must invoke a fallback plan. See finding L-14 and runbook §9. |
| `error` | `{ "t": "error", "message": string }` | **Fatal.** The session is over. The client shows an error banner and reveals **Reconnect**. |
| `pong` | `{ "t": "pong" }` | Reply to `ping`. |

### `ReadyConfig`

Sent inside `ready`. Serialised from an anonymous object (`VoiceLiveWebSocketBridge.cs:109-117`) with `JsonSerializerDefaults.Web` (camelCase). The TypeScript type is `ReadyConfig` (`views.ts:1`).

| Field | Type | Notes |
|---|---|---|
| `mode` | string | Configured mode (`"model"` or `"agent"`). |
| `activeMode` | string | Turn-taking mode actually in force: `"gated"`, `"open-mic"`, or `"hybrid"`. |
| `agentName` | string | Agent name. Required in all modes; `ServerSessionConfig.cs:104` calls `RequireServer` unconditionally. |
| `safeQuestions` | string[] | Rendered as one-click buttons in the operator view. |
| `avatarCharacter` | string | Avatar character id. |
| `avatarStyle` | string | Avatar style id. |

### `ClientConfig`

Returned by `GET /api/config`. Serialised from the `ClientConfig` record (`WebConfig.cs:7-16`) by ASP.NET Core minimal APIs with `JsonSerializerDefaults.Web` (camelCase).

| Field | Type | Notes |
|---|---|---|
| `region` | string | Azure region for the Voice Live endpoint. |
| `apiVersion` | string | API version string (e.g. `"2025-10-01"`). |
| `model` | string | Model deployment name. |
| `voice` | `{ type: string, name: string }` | Voice type and name. |
| `avatar` | object | Raw avatar configuration (passed through as a `JsonElement`). |
| `activeMode` | string | Turn-taking mode in force (`"gated"`, `"open-mic"`, or `"hybrid"`). |
| `agentName` | string | Agent name. |
| `agentProjectName` | string | Agent project name. |
| `safeQuestions` | string[] | Safe-question allow-list. |

### `IceServer`

Sent inside `ready.iceServers`.

| Field | Type | Notes |
|---|---|---|
| `urls` | string[] | STUN/TURN server URLs. |
| `username` | string? | Optional TURN credential username. |
| `credential` | string? | Optional TURN credential password. |

## Per-view frame restrictions

| View | URL | `start-turn` / `end-turn` | `barge-in` | `say` | Notes |
|---|---|---|---|---|---|
| Landing | `/` | **Gated mode only** | **No** | **No** | Default fullscreen avatar. `wireInteractiveControls` binds pointer handlers only when `activeMode === "gated"`; in other modes the mic streams continuously. No `stopButton`, `repeatButton`, or `safeQuestionButtons`. |
| Operator | `/?view=operator` | **Gated mode only** | **Yes** | **Yes** | Full control console with transcripts, safe questions, barge-in, tool activity. All interactive buttons are present. |
| Display | `/?view=display` | **No** | **No** | **No** | `prepareMicrophone` and `wireInteractiveControls` return immediately for non-interactive views. Opens a WebSocket and receives `ready`; initiates WebRTC negotiation for avatar video; cannot send any turn frame. |

## Validation

**Neither side validates frame shape today.** Both ends switch on `t` and read fields optimistically, so a malformed frame produces an undefined-property error rather than a clean protocol failure. Tracked as finding M-06. This table is the contract that fix should enforce.
