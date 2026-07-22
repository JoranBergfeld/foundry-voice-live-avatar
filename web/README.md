# VoiceLive.Web

ASP.NET Core backend for the Foundry Voice Live avatar MVP.

## Architecture

The web backend has three responsibilities:

- **Config endpoint**: `/api/config` reads the repository `config/` JSON files and returns only browser-safe fields needed by the frontend.
- **Token broker**: `/api/token` obtains a short-lived Microsoft Entra access token for `https://ai.azure.com/.default` using `DefaultAzureCredential`.
- **Static hosting**: the app serves future frontend assets from `wwwroot` with default-file and static-file middleware.

The Voice Live avatar session and avatar rendering run in the browser in a later frontend phase. The backend does not proxy the live media/session stream in this MVP.

## Endpoints

- `GET /api/health` returns `{"status":"ok"}`.
- `GET /api/config` returns sanitized config: region, API version, model, voice, avatar settings, active turn-taking mode, agent metadata, and safe questions.
- `GET /api/token` returns `{ "token": "...", "expiresOn": "..." }` or HTTP 502 with a clear error if no Azure credential is available.

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

The browser never receives the Foundry endpoint from `session.json`. The only credential material sent to the browser is a short-lived Entra access token from `/api/token`; no fake token is returned when Azure authentication fails.
