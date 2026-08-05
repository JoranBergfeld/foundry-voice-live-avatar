# Session flow and state

How a session starts, how a turn runs, what the status indicators mean, and what each view can do. For frame payloads see [`wire-protocol.md`](wire-protocol.md).

## Connection flow

![Voice Live connection and pre-warm flow](images/voice_live_prewarm_connection_flow.png)

The browser holds no Azure credential at any point. The server acquires the token, opens the upstream session, and only then tells the browser it is `ready`. Avatar media is negotiated afterwards and flows directly browser↔Azure over WebRTC.

## A single turn

![Voice Live single turn flow](images/voice_live_single_turn_flow.png)

> **Image note:** the diagram's "WS deltas" box is inaccurate. The WebSocket carries **text frames only** (transcripts, state, tool and error events); all avatar audio and video arrive over the WebRTC media plane. The WebSocket never forwards audio to the browser.

In **gated** mode (the shipped default) a turn is explicitly bracketed by the operator:

1. Operator presses **Hold to talk** → client sends `start-turn` and begins streaming microphone audio.
2. Binary PCM16 frames flow while the button is held. Turn detection is `NoTurnDetection` in gated mode (`SessionOptionsBuilder.cs:71`): **no `speech-started`/`speech-stopped` events are emitted** and the server does not segment the audio by VAD.
3. Operator releases → client sends `end-turn` and stops streaming audio. Because `InputAudioTranscription` is not set when `UsesTurnDetection` is false (`SessionOptionsBuilder.cs:35-41`), **no `user-transcript` frames are emitted in gated mode**.
4. The model responds: `agent-transcript` frames stream in, `avatar-speaking` fires when audio playback begins, avatar video and audio arrive over the WebRTC media plane.
5. `avatar-idle` then `response-done` close the turn.

In **`open-mic`** and **`hybrid`** modes (where `UsesTurnDetection` is true), the server emits `speech-started`/`speech-stopped` VAD events and `user-transcript` frames. `final: false` frames carry a delta that **appends** to the live transcript line; the `final: true` frame carries the complete transcript and **replaces** the accumulated text.

**Safe questions** (operator view only) skip the turn steps above: clicking one sends a single `say` frame and the flow resumes at the model-response stage.

**Barge-in** (operator view only) sends `barge-in` during model speech to interrupt the avatar.

### Turn-taking modes

| Mode | How turns start | `start-turn` / `end-turn` sent? | VAD segments turns? |
|---|---|---|---|
| `gated` | Hold to talk (default) | Yes, by the operator | No |
| `open-mic` | Automatically on `ready` | No | Yes — Azure semantic VAD |
| `hybrid` | Automatically on `ready` | No | Yes — Azure semantic VAD |

The active mode is reported in the `ready` frame as `activeMode`. `wireInteractiveControls` in `main.ts` binds the pointer handlers for Hold to talk only when `activeMode === "gated"`; in `open-mic` and `hybrid` the microphone streams continuously from the moment it is ready.

### Rules and edge cases

- **Wait for `ready`.** The client must not send anything before `ready`; the server does not enforce this gate, but frames sent in that window may be processed in undefined state.
- **Mute** is available in `open-mic` and `hybrid` modes on the landing view only (`views.ts:336`, `main.ts:212-214`). It toggles microphone streaming (`streamingMic`) and is structurally impossible in gated mode or from the operator view. **There is no mute control on the operator view.**
- **Barge-in outside avatar speech** is harmless but pointless — there is nothing to interrupt.
- **`barge-in` and `say` are operator-view only.** The landing view wires neither; it has no `stopButton`, `repeatButton`, or `safeQuestionButtons`. See the per-view table in [`wire-protocol.md`](wire-protocol.md).

## Decision points

![Voice Live decision points](images/voice_live_decision_points.png)

> **Image note:** the connection-drop branch in this diagram depicts an automatic freeze-and-retry with a "Fallback video" path. **This is aspirational, not shipped.** The actual code performs full teardown and reveals a manual **Reconnect** button. There is no automatic reconnect loop and no fallback video asset in the repository.

The diagram shows three decision paths:
1. **Barge-in** (speech during avatar response) → check turn-taking config → cancel response (open-mic/hybrid) or ignore input (gated: reply finishes first).
2. **Repeat request** → recall last reply text → re-synthesize.
3. **Connection drop** (WebSocket or WebRTC) → **aspirational, not shipped**: the diagram depicts an automatic freeze-and-retry with a fallback-video branch; in the actual code, `disconnect()` performs full teardown (closes socket and peer connection, stops all tracks, calls `setDisconnected(true)`) and exposes a manual **Reconnect** button. There is no automatic reconnect, no freeze loop, and no fallback video asset. See line below and [`wire-protocol.md`](wire-protocol.md).

## Status channels

The operator view exposes six independent status channels. The landing view surfaces only `connection` and `webrtc` in a transient pill. The display view collapses all three of `connection`, `webrtc`, and `avatar` into a single status string.

| Channel | Representative values | Meaning when unhealthy |
|---|---|---|
| `connection` | `ready` (healthy); `connecting`; `connected; waiting for ready`; `disconnected` | WebSocket to the app is down. The **Reconnect** button appears; nothing else works until reconnected. |
| `webrtc` | `connected` (healthy); `creating peer connection`; `offer sent; waiting for answer`; `failed`; `avatar disabled (capacity)` | Media-plane failure. Both avatar audio and video are lost because both transceivers ride the same peer connection — closing it ends all inbound media. **There is no voice-only fallback.** The WebSocket, microphone, and transcripts all survive; the room loses all audible output. See [runbook.md](runbook.md) §9. |
| `microphone` | `ready` / `live` (healthy); `requesting permission`; `muted` | Microphone setup **failed fatally**: `prepareMicrophone` catches `getUserMedia` errors and calls `disconnect()`, which closes the WebSocket, stops all tracks, calls `setReady(false)` and `setDisconnected(true)`. Every button is disabled; **Reconnect** is the only recovery. `muted` (landing view, non-gated only) is non-fatal — mic streaming is paused but the session is alive. |
| `turn` | `gated: hold to talk` / `open-mic: streaming continuously` (idle); `recording gated turn` (active) | Stuck on `recording gated turn` → a `start-turn` was never closed; release and re-press Hold to talk. |
| `speech` | `started` / `stopped` | Server-side VAD. **Only emitted in `open-mic` and `hybrid` modes** — gated mode uses `NoTurnDetection` and never emits these events. In open-mic/hybrid, never showing `started` while speaking → the microphone is muted or capturing silence. In gated mode this channel never leaves its initial state; that is normal, not a fault. |
| `avatar` | `speaking` / `idle` (healthy); `unavailable` | `unavailable` means an `avatar-error` frame was received. Check the `webrtc` channel for details. |

## The three views

All three are the same app shell, selected by query string, and **each open tab is its own session consuming one concurrency slot**. `SessionGate` is a singleton `SemaphoreSlim` backed by `VoiceLiveOptions.MaxConcurrentSessions` (default `2`, configured in `appsettings.json`). One WebSocket upgrade = one slot acquired; closing the WebSocket releases it.

| View | URL | Microphone | Controls | Intended screen |
|---|---|---|---|---|
| Landing | `/` | Yes | Hold to talk (gated mode only); mute toggle (non-gated modes only, same button); Reconnect (on disconnect); the ⚙ gear is the only route to the operator view | Setup and testing |
| Operator | `/?view=operator` | Yes | Hold to talk (gated only), safe questions, barge-in, all six status channels, Reconnect. **No mute control** (`controls.append` in `views.ts:148` is `holdButton, stopButton, repeatButton, safeQuestionPanel`). | The operator's laptop, never visible to the audience |
| Display | `/?view=display` | **No** | Avatar video only; Reconnect appears on disconnect | The stage screen |

**Two consequences worth planning for:**

- The display view has no microphone and no interaction affordance, yet a browser will still block autoplay until the page receives a user gesture. The app calls `play()` on an unmuted element; a `NotAllowedError` from the browser tears down the whole Voice Live session (not just the video) and shows a fatal error banner. **Always click into the display screen once before the audience arrives.** Recovery: click **Reconnect** (the click satisfies the gesture requirement and restarts the session); reload only if Reconnect still fails. See [runbook.md](runbook.md) §7.
- Reconnection is operator-initiated; there is no automatic reconnect. An unattended display screen that disconnects stays disconnected until someone clicks Reconnect.
