# Agent Session Configuration Ownership Design

## Goal

Prevent local application configuration from overriding a hosted Voice Live agent, so the same agent configuration is used by local and deployed web app instances.

## Ownership

In agent mode, the web app starts the session with `SessionTarget.FromAgent(...)` and sends no `session.update`. The hosted agent owns model, instructions, tools, voice, avatar, audio formats, transcription, noise handling, and turn detection.

In model mode, the web app continues to build and send the complete `VoiceLiveSessionOptions` from repository configuration. Browser-only behavior, authentication, safe-question controls, WebSocket transport, and WebRTC rendering remain local application responsibilities in both modes.

## Implementation

`VoiceLiveSessionFactory` will configure only model-mode sessions. `SessionOptionsBuilder.BuildForAgent` will be removed so a future caller cannot accidentally restore local agent overrides through that API.

The existing configuration files remain available for model mode and browser-safe projection. This change does not redesign the configuration schema or frontend payload.

## Verification

A focused unit test will assert that no session options are produced for agent mode and that model mode still produces its configured options. The complete web test suite will guard session behavior and documentation consistency.