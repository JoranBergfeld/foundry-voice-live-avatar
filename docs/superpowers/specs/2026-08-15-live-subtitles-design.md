# Live Subtitles Design

## Goal

Let viewers enable or disable live subtitles for agent speech on the landing and
display views. Subtitles are disabled by default and use transcript events that
the application already receives from Voice Live.

## Scope

The feature covers:

- Agent speech only.
- The landing view and passive display view.
- A browser-side subtitle toggle in each supported view.
- Live delta rendering, final transcript replacement, and automatic fade-out.

User speech subtitles, operator-view subtitles, transcript persistence, backend
configuration changes, and captions burned into avatar media are out of scope.
The existing operator and landing transcript history remains unchanged.

## Architecture

The browser already receives `agent-transcript` frames containing live deltas
and final transcript text. `ThinVoiceLiveClient` will continue routing those
frames to transcript history where the view supports it and will additionally
route agent text to a dedicated subtitle presentation API.

The landing and display views will each own:

- A `Subtitles` toggle with an `aria-pressed` state.
- A high-contrast subtitle overlay positioned over the lower part of the avatar.
- The enabled state and subtitle lifecycle timers.

This separation keeps subtitle presentation independent from transcript history.
No changes are required to Azure session options, server-side update handling,
or the WebSocket wire protocol.

## User Experience

Subtitles start disabled whenever a page is newly loaded. Enabling the toggle
shows subsequent agent transcript text in a bottom-centered overlay. Disabling
the toggle immediately hides and clears visible subtitle text.

The overlay accumulates live transcript deltas for the current response. A final
transcript frame replaces the accumulated text with the authoritative complete
transcript. On `response-done`, the final subtitle remains visible for three
seconds and then fades out and clears.

Starting a new response cancels any pending fade or clear operation. A WebSocket
disconnect or fatal error also clears subtitle text and cancels timers. A
reconnect within the same page preserves the user's current toggle choice;
reloading or opening a new page restores the disabled default.

The toggle uses native button behavior and exposes its state through
`aria-pressed`. The overlay uses readable contrast and responsive width so it
does not obscure more of the avatar than necessary.

## Component Changes

### View contracts

Extend the browser view contracts with focused methods for:

- Reading or changing subtitle enabled state.
- Applying live or final agent transcript text.
- Completing a response so the fade lifecycle can begin.
- Clearing subtitle presentation during teardown.

Define a subtitle-capable view interface and use a type guard before routing
subtitle events. The landing and display views implement that interface; the
operator view remains outside it. The display view remains non-interactive for
voice turns, and its subtitle toggle is a local presentation control only.

### Client event routing

For `agent-transcript` frames, the client will:

1. Preserve existing transcript-history behavior for interactive views.
2. Forward the text and `final` flag to subtitle-capable views.

For `response-done`, the client will preserve existing status updates and notify
the subtitle-capable view to schedule the three-second fade. Client teardown
will notify the view to clear subtitle state.

### View rendering

Landing and display rendering will create the toggle and subtitle overlay using
the existing DOM-construction style in `views.ts`. Styling will be added to the
application shell in `wwwroot/index.html`, following current responsive landing
and display patterns.

## Error Handling

Subtitle failures must never terminate or degrade the Voice Live session.
Empty transcript deltas are ignored. Timer callbacks must be invalidated when a
new response starts, subtitles are disabled, or the session disconnects, so
stale callbacks cannot hide or restore text from a newer response.

Existing malformed-frame and transport error handling remains authoritative.
The feature introduces no new server error shape or fallback path.

## Testing

Playwright tests will cover both supported views:

- Subtitles are disabled and hidden by default.
- The toggle has the correct accessible name and `aria-pressed` state.
- Enabling subtitles renders accumulated live agent deltas.
- A final frame replaces accumulated text.
- `response-done` keeps final text visible for three seconds, then fades and
  clears it.
- A new response cancels a pending fade.
- Disabling subtitles clears text immediately.
- Disconnect and reconnect do not allow stale subtitle timers or text to leak.
- The existing landing transcript panel still receives agent transcript events.
- Display-view subtitle controls do not enable microphone or turn controls.

Type checking will validate the extended view contracts. The targeted frontend
test suite will validate browser behavior and responsive presentation.
