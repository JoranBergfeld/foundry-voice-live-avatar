# Rehearsal Checklist

## Day before

- [ ] Confirm the Azure AI Foundry resource is in `swedencentral`.
- [ ] Confirm app/managed-identity RBAC: `Cognitive Services User` + `Foundry User` on the account/project scope.
- [ ] Run `az login` on the operator machine for local runs, or confirm `azd up` has deployed the App Service managed identity.
- [ ] Review `/config` and app settings against [`docs/config-schema.md`](config-schema.md).
- [ ] Finalize `config/grounding/company-direction.md`.
- [ ] Confirm safe questions in `config/agent.json`.
- [ ] Run the web app locally or confirm the deployed URL is available:
  ```bash
  ConfigDir=$(pwd)/config dotnet run --project web/src/VoiceLive.Web
  ```
  > Run from the repository root. `ConfigDir` is required — `dotnet run` sets the working directory to the project directory, so without it the config files are not found and `/api/health` returns 503. `VoiceLive__Endpoint` and the operator credentials must also be set; see [CONTRIBUTING.md](../CONTRIBUTING.md).
- [ ] Open `/?view=operator`, sign in, grant microphone permission, and ask a safe question with hold-to-talk.

## Event-day setup

- [ ] Confirm local `az login` is still valid, or confirm the deployed App Service is healthy.
- [ ] Start the local web app or open the deployed URL:
  ```bash
  ConfigDir=$(pwd)/config dotnet run --project web/src/VoiceLive.Web
  ```
  > Run from the repository root. Omitting `ConfigDir` starts the app unhealthy.
- [ ] Check health: `curl -s http://localhost:5280/api/health`. Expect `Healthy`; `Unhealthy` (HTTP 503) means config did not load — check `ConfigDir` and `VoiceLive__Endpoint` first.
- [ ] Sign in with the configured operator credentials at `http://localhost:5280/login` or `<deployed-url>/login`.
  > **Why sign in first:** unauthenticated requests are redirected to `/login` with no `ReturnUrl`, so opening `/?view=operator` or `/?view=display` before sign-in silently discards the query string and lands you on `/login` instead. Sign in first; then navigate to the views.
- [ ] After sign-in the browser lands on `/`. Navigate that tab to `/?view=operator` (operator view).
  > Signing in always redirects back to `/`, which loads the landing page and consumes a session slot. Navigating away to `/?view=operator` fires `beforeunload` → `client.dispose()`, releasing the landing session before the operator view opens — so only **1 session** is live after this step. Do **not** leave the landing page open alongside the operator view.
  > **Session cap:** the server allows **2 concurrent sessions** by default (`MaxConcurrentSessions` in `appsettings.json`). Every open tab that loads a view consumes one slot. Do **not** open a third tab. To raise the cap for multi-display setups, increase `MaxConcurrentSessions` in `appsettings.json` (or the equivalent App Service application setting) before deploying.
- [ ] Grant microphone permission in the operator browser.
- [ ] **Open and authenticate the display tab.** If the display screen is a separate machine or browser profile (the normal venue setup), navigate to `/?view=display` on that machine, sign in with the configured operator credentials, then navigate to `/?view=display` again (sign-in always redirects back to `/`). If the display tab is in the same browser profile as the operator tab, the session cookie is already shared — open a new tab and navigate directly to `/?view=display` (no second sign-in needed).
  > With the operator tab already open (1 session), the display tab is the second and last slot.
- [ ] **Click anywhere on the display screen once** to satisfy the browser autoplay/user-gesture requirement for that tab. (User activation is per-document; a gesture in the operator tab does not cover the display tab.)
- [ ] Confirm avatar **video** arrives in the display tab and the connection status shows healthy. The avatar will be **idle and silent** — this is correct and expected. The display tab runs its own independent Voice Live session that receives no microphone input and no operator controls, so it never produces speech. **Room audio comes from the operator machine only.** Do not spend time trying to make the display tab speak; it cannot.
- [ ] In the **operator tab**: click a safe question or use hold-to-talk once to satisfy browser autoplay/user-gesture requirements for that tab.
- [ ] Confirm avatar video and audio arrive in the operator tab.
- [ ] Confirm one safe question completes end-to-end with streaming transcript and final response.

## During-show controls

- [ ] Use **Hold to talk** for live operator input.
- [ ] Use safe-question buttons to steer back to approved topics.
- [ ] Use repeat to replay the last completed answer when needed.
- [ ] Use barge-in/interrupt controls if the avatar needs to stop speaking.
- [ ] If a **fatal error banner** appears and a **Reconnect** button is visible, the session has closed; click **Reconnect** to restore it (this preserves sign-in, mic permission, and autoplay gesture). Reload/restart the tab only if Reconnect fails.
- [ ] If an **"Avatar unavailable"** notice appears but **no Reconnect button is present**, the avatar connection failed non-fatally — **however, avatar audio is also lost along with the video**, because both ride the same WebRTC peer connection. The WebSocket, microphone capture, and transcripts continue, but there is no audible output to the room. Do NOT click Reconnect and do NOT reload the tab (reloading destroys the transcript session). **Invoke your fallback plan** (hand off to a human presenter, or restart against an avatar-capable resource). See the runbook for recovery options.

## Known limitations to brief stakeholders

- [ ] Each browser tab opens its own `/ws/session`; the display tab therefore runs an independent session whose avatar does **not** mirror the operator's conversation and produces **no** audio. Room audio must come from the operator machine. Shared operator+display rooms are future work.
- [ ] The server allows **2 concurrent sessions** by default (`MaxConcurrentSessions` in `appsettings.json`). A third simultaneous tab is rejected immediately with a startup error. To support more displays, raise `MaxConcurrentSessions` before deploying.
- [ ] Agent mode is opt-in and requires a Voice Live agent created in the Azure AI Foundry portal.
- [ ] Browsers require a user gesture before video/audio autoplay; the operator should sign in, grant mic permission, then press a control before showtime.
- [ ] A `?view=display` screen that disconnects (e.g., from blocked autoplay or a network drop) stays in the disconnected state until a human clicks the **Reconnect** button. For an unattended display, ensure a human is present to respond if a disconnection occurs.
