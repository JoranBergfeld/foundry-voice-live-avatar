# VoiceLive.Web

ASP.NET Core backend for the Foundry Voice Live avatar MVP.

## Architecture

The web backend has three responsibilities:

- **Config endpoint**: `/api/config` reads the repository `config/` JSON files and returns only browser-safe fields needed by the frontend.
- **Token broker**: `/api/token` obtains a short-lived Microsoft Entra access token for `https://ai.azure.com/.default` using `DefaultAzureCredential`.
- **Static hosting**: the app serves future frontend assets from `wwwroot` with default-file and static-file middleware.

**Web architecture (Option A)**: the ASP.NET app hosts the Voice Live session server-side through the Azure.AI.VoiceLive .NET SDK and bridges browser control/audio over `/ws/session`. The Voice Live credential never leaves the server. Avatar media still flows browser <-> Azure over WebRTC; the server only relays the SDP offer/answer and ICE server metadata described in the phase wire protocol.

## Endpoints

- `GET /api/health` returns `{"status":"ok"}`.
- `GET /api/config` returns sanitized config: region, API version, model, voice, avatar settings, active turn-taking mode, agent metadata, and safe questions.
- `GET /api/token` returns `{ "token": "...", "expiresOn": "..." }` or HTTP 502 with a clear error if no Azure credential is available.
- `WS /ws/session` starts a server-side Voice Live session. Browser binary frames are PCM16 audio; browser JSON controls include `avatar-offer`, `start-turn`, `end-turn`, `barge-in`, `say`, and `ping`. Server JSON events include `ready`, transcripts, speech/avatar state, `avatar-answer`, `response-done`, and `error`.

## Run locally

From the web app directory:

```bash
cd src/VoiceLive.Web
ConfigDir=../../../config ASPNETCORE_URLS=http://localhost:5280 dotnet run
```

In another shell:

```bash
curl -s http://localhost:5280/api/health; echo
curl -s http://localhost:5280/api/config; echo
curl -s http://localhost:5280/api/token; echo
```

If `/api/token` returns a credential error, run `az login` or configure a managed identity.

## Security

The browser never receives the Foundry endpoint from `session.json` or the Voice Live credential. `/ws/session` uses `DefaultAzureCredential` only on the server. `/api/token` remains for compatibility and returns a clear error instead of a fake token when Azure authentication fails.
