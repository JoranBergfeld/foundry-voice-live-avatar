# Voice Live WebSocket Bridge Refactor Design

## Goal

Reduce the size and responsibility of `VoiceLiveWebSocketBridge` without changing session behavior, browser wire frames, logging semantics, metrics, or its public API.

## Design

`VoiceLiveWebSocketBridge` remains the orchestration boundary. It owns the per-session logging scope, metrics, linked cancellation lifetime, concurrent pump coordination, and session disposal.

Four focused collaborators take over the implementation details:

- `VoiceLiveSessionFactory` creates the SDK client, starts either an agent or model session, and applies the correct session options.
- `VoiceLiveUpdateHandler` translates Voice Live SDK updates into browser frames. It owns ready-state initialization, avatar SDP answers, transcripts, tool notifications, and service error handling.
- `BrowserMessageHandler` dispatches parsed browser control messages to the Voice Live session. It owns audio-turn IDs and the model-versus-agent behavior for `say` messages.
- `WebSocketTransport` assembles incoming WebSocket messages, enforces the message-size limit, serializes outgoing JSON under a send lock, and closes the socket safely.

Dependencies are passed explicitly. The collaborators remain internal implementation details and do not introduce new application-level interfaces unless tests require a narrow seam around an SDK type that cannot be constructed directly.

## Data Flow

1. The bridge asks `VoiceLiveSessionFactory` for a configured session.
2. The bridge starts the Voice Live update pump and browser message pump concurrently.
3. `VoiceLiveUpdateHandler` consumes each service update and writes browser frames through `WebSocketTransport`.
4. `WebSocketTransport` reads complete browser messages; binary audio goes to the session and text control frames go to `BrowserMessageHandler`.
5. Completion of either pump cancels the shared lifetime; the bridge awaits both pumps and disposes the session.

## Preserved Behavior

- Agent and model startup use their current targets and option builders.
- All existing browser frame shapes and tool phases remain unchanged.
- The one-megabyte browser message limit and close statuses remain unchanged.
- Malformed or unknown control frames remain ignored.
- Avatar capacity errors remain nonfatal and retain their operator-facing message.
- Other service errors still emit an error frame and close the WebSocket.
- Cancellation and WebSocket exceptions retain their current handling.
- Existing metrics and session-scoped logging remain on the bridge.

## Testing

The refactor follows characterization-first TDD. Focused tests establish the extracted collaborators' current behavior before production extraction. Existing control-message, avatar SDP, capacity-error, tool-notification, configuration, and integration tests remain authoritative for compatibility.

Validation runs the focused `VoiceLive.Web.Tests` tests after each extraction and the complete test project after the collaborators are wired together.

## Non-Goals

- Changing the browser wire protocol.
- Adding tool execution or changing Agent mode.
- Changing authentication, configuration, retries, or error policy.
- Redesigning the frontend or Voice Live session options.