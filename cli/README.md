# Voice Live CLI

The CLI is the rehearsal harness for the Foundry Voice Live avatar MVP. It validates the shared JSON configuration and can run a live Azure Voice Live model-mode session for headless checks.

## Requirements

- .NET 10 SDK
- Azure login available to `DefaultAzureCredential` (for example, `az login`)
- Windows for live microphone capture/playback, because the audio layer uses NAudio `WaveInEvent`/`WaveOutEvent`.

## Validate configuration

From the repository root:

```bash
dotnet run --project cli/src/VoiceLive.Cli -- validate --config config
```

The command loads `session.json`, `turntaking.json`, and `agent.json`, fails fast with file/field validation errors, and prints the resolved legacy `session.update` payload. In gated/manual mode, `turn_detection` is intentionally omitted from that payload.

## Run a live Voice Live session

Model mode is the default and uses the configured endpoint/model with `DefaultAzureCredential`:

```bash
dotnet run --project cli/src/VoiceLive.Cli -c Release -- run --config config --mode model --text "Say hello in one short sentence." --seconds 30
```

Headless modes work on Linux/WSL and Windows:

- `--text <prompt>` configures a session, starts a response, prints transcript deltas, and reports final transcript, audio bytes, and first-audio latency.
- `--audio-file <path>` streams a PCM16 mono 24 kHz WAV file, commits the input audio, starts a response, and prints transcript/latency details.

If neither `--text` nor `--audio-file` is supplied, the CLI attempts live microphone/speaker mode. That mode is Windows-only; on non-Windows platforms the CLI exits with a clear message telling you to use `--text` or `--audio-file`.

Agent mode plumbing is present for later phases:

```bash
dotnet run --project cli/src/VoiceLive.Cli -c Release -- run --config config --mode agent --text "Hello" --seconds 30
```

It compiles against the real SDK but is not live-verified until the Foundry agent is created by the later sync flow.

## Config hot reload

The planned runtime will use smart reload: watch the config directory, validate changed files, and apply safe session updates without restarting when possible. Changes that require a new live session will be reported explicitly so the operator can restart cleanly instead of silently masking incompatible changes.
