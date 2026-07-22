# Voice Live CLI

The CLI is the rehearsal harness for the Foundry Voice Live avatar MVP. It validates the shared JSON configuration and, in later phases, will run local operator rehearsals against the live Voice Live session.

## Requirements

- .NET 10 SDK
- Windows for audio capture/playback once live audio is implemented, because the planned audio layer uses NAudio.

The current MVP CLI has no Azure SDK dependencies and can validate configuration offline.

## Validate configuration

From the repository root:

```bash
cd cli/src/VoiceLive.Cli
dotnet run -- validate --config ../../../config
```

The command loads `session.json`, `turntaking.json`, and `agent.json`, fails fast with file/field validation errors, and prints the resolved `session.update` payload. In gated/manual mode, `turn_detection` is intentionally omitted from that payload.

## Planned commands

- `run`: start the rehearsal harness and connect to the Voice Live API.
- `sync-agent`: synchronize Foundry Agent metadata or grounding assets into the local rehearsal flow.

Audio capture, playback, and live-session connectivity are Phase 7 work and are not part of this MVP CLI slice.

## Config hot reload

The planned runtime will use smart reload: watch the config directory, validate changed files, and apply safe session updates without restarting when possible. Changes that require a new live session will be reported explicitly so the operator can restart cleanly instead of silently masking incompatible changes.
