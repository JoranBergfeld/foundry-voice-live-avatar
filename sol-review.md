# Repository Implementation Review

**Reviewed:** 2026-08-05  
**Scope:** All tracked application code, frontend code, tests, configuration, infrastructure, deployment scripts, CI, and current operational documentation.

## Executive summary

The implementation is generally well structured and has strong baseline controls around authentication, browser-safe configuration, WebSocket origin checks, managed identity, RBAC-only Azure AI access, bounded WebSocket messages, session cleanup, security headers, strict TypeScript, and automated tests.

No Critical or High severity issues were identified. The review found **4 Medium** and **9 Low** severity issues. The most important risks are avatar autoplay failure in unattended views, storage of the shared login password in App Service settings, missing CI security/IaC gates, and the absence of session lifetime or idle limits.

## Validation results

| Check | Result |
|---|---|
| .NET tests | 90/90 passed |
| Frontend TypeScript check | Passed |
| Playwright Chromium tests | 23/23 passed |
| NuGet vulnerability audit, including transitive packages | No known vulnerabilities |
| npm production dependency audit | No known vulnerabilities |
| npm full dependency audit | No known vulnerabilities |
| Repository state | Clean; no tracked common build artifacts or secret-like filenames found |

Validation limitations: browser tests cover Chromium only; advisory audits reflect currently known vulnerabilities; the secret check was filename-based rather than a dedicated content scan; .NET tests intentionally skipped rebuilding the frontend.

## Findings

### MED-01: Avatar autoplay can make unattended display and landing views unusable

**Evidence:** `web/frontend/src/main.ts:214-224`, `web/frontend/src/views.ts:107-111`, `web/frontend/src/views.ts:275-281`, `web/frontend/src/views.ts:404-408`, `web/frontend/tests/browser-mocks.ts:334-340`

The avatar media stream includes audio, but the application calls `video.play()` programmatically on first connection while the video is not muted. Browsers commonly reject media-with-audio autoplay until a user gesture occurs. Any non-`AbortError` rejection disconnects the complete session.

This is particularly harmful to `?view=display`, which is intended as an unattended kiosk and provides no initial interaction. The current browser mock always resolves `play()`, so the failure path is not tested.

**Recommendation:** Treat `NotAllowedError` as a recoverable media condition. Retry with the avatar muted so video can start, then expose a user-gesture control to enable audio. Do not tear down the Voice Live session solely because autoplay was blocked. Add an end-to-end test in which `play()` initially rejects.

### MED-02: The shared application password is stored directly in App Service settings

**Evidence:** `infra/main.bicep:13-16`, `infra/resources.bicep:4-6`, `infra/resources.bicep:88-89`

The Bicep parameter is correctly marked `@secure()`, which protects normal deployment output, but the deployed password is still written as the value of `Auth__Password` in App Service configuration. Principals that can list application settings can retrieve the sole operator credential, and the value is not independently managed or rotated.

**Recommendation:** Store the password in Azure Key Vault and use a Key Vault reference in the App Service setting. Grant the Web App managed identity only `Key Vault Secrets User`, document rotation, and avoid granting broad access to application configuration.

### MED-03: CI lacks explicit supply-chain, dependency, and infrastructure gates

**Evidence:** `.github/workflows/ci.yml:1-28`

CI runs unit tests, TypeScript checking, the frontend build, and Chromium Playwright tests, but it does not:

- declare explicit least-privilege `GITHUB_TOKEN` permissions;
- pin third-party actions to immutable commit SHAs;
- run NuGet/npm vulnerability checks;
- run CodeQL or another static analysis tool;
- validate Bicep compilation or deployment configuration;
- scan repository content for committed secrets.

The manual dependency audits performed during this review were clean, but they are not enforced on future changes.

**Recommendation:** Add `permissions: { contents: read }`, pin actions to commit SHAs, configure Dependabot, add dependency and secret scanning, enable CodeQL, and add an `az bicep build` validation job. Add broader browser coverage where display reliability requires it.

### MED-04: Authenticated clients can hold every Voice Live session slot indefinitely

**Evidence:** `web/src/VoiceLive.Web/Program.cs:141-172`, `web/src/VoiceLive.Web/Session/VoiceLiveWebSocketBridge.cs:28`, `web/src/VoiceLive.Web/Session/VoiceLiveWebSocketBridge.cs:205-236`, `web/src/VoiceLive.Web/appsettings.json:16`

The application limits concurrent sessions to two, but it does not enforce an idle timeout or maximum session duration. An authenticated client can retain both WebSocket and upstream Azure Voice Live sessions indefinitely, exhausting all capacity and potentially generating unnecessary service cost.

The 1 MiB message limit protects individual messages but does not mitigate idle capacity exhaustion. Existing tests cover gate accounting, not long-lived or inactive sessions.

**Recommendation:** Add configurable idle and absolute session timeouts using the linked cancellation token. Record timeout termination reasons, alert on sustained capacity saturation, and test idle-slot reclamation.

### LOW-01: Wrong JSON value types terminate the caller's session

**Evidence:** `web/src/VoiceLive.Web/Session/VoiceLiveWebSocketBridge.cs:244-282`

Malformed JSON is ignored safely, but well-formed messages such as `{"t":"say","text":123}` or `{"t":"avatar-offer","sdp":123}` call `JsonElement.GetString()` on a non-string value. This throws `InvalidOperationException`, reaches the bridge-wide exception handler, and closes the client's session.

**Recommendation:** Check `ValueKind == JsonValueKind.String` before reading `text`, `sdp`, and `t`. Reject or ignore invalid control frames consistently and add protocol-level tests for incorrect field types.

### LOW-02: Upstream service error details are forwarded to the browser

**Evidence:** `web/src/VoiceLive.Web/Session/VoiceLiveWebSocketBridge.cs:179-198`

Non-capacity `SessionUpdateError` messages include the upstream service's raw error message in the client-facing response. Other exception paths use a sanitized error helper, making this path inconsistent and potentially disclosing service internals to authenticated users.

**Recommendation:** Return a stable generic client message and a documented error code. Log the complete upstream message only on the server with the session correlation identifier.

### LOW-03: Malformed `ready` frames can leave the UI silently stuck

**Evidence:** `web/frontend/src/main.ts:34-38`, `web/frontend/src/main.ts:130-206`, `web/frontend/src/views.ts:196-208`

`parseServerFrame` validates only the `t` discriminator and casts the remaining object. A `ready` frame without a valid `config` or `safeQuestions` value can throw during `onReady` or `setConfig`. The resulting rejected message handler is not routed through `disconnect()`, so the user may receive neither an error nor a reconnect control.

The backend currently emits valid frames, so this is primarily resilience against future protocol drift or backend defects.

**Recommendation:** Validate each server frame's required fields before dispatch, or use a small schema validator. Route validation failures through the normal disconnect/error flow and test malformed frames.

### LOW-04: PCM downsampling uses unfiltered nearest-neighbor decimation

**Evidence:** `web/src/VoiceLive.Web/wwwroot/pcm-worklet.js:18-35`, `web/frontend/tests/pcm-worklet.spec.ts`

When the browser ignores the requested 24 kHz `AudioContext` rate and supplies 44.1 or 48 kHz audio, the worklet selects individual source samples without a low-pass filter. Frequencies above the target Nyquist frequency can alias into the captured signal and reduce speech-recognition quality.

The tests verify the current sample-selection algorithm rather than audio fidelity.

**Recommendation:** Apply a low-pass filter before decimation or use a windowed/averaging resampler. Add signal-based tests that measure attenuation above 12 kHz and preserve expected speech-band frequencies.

### LOW-05: Login comparison timing varies with credential length

**Evidence:** `web/src/VoiceLive.Web/Auth/LoginEndpoints.cs:40-50`

`CryptographicOperations.FixedTimeEquals` is constant-time for equal-length buffers but returns quickly when lengths differ. An attacker could theoretically infer configured username and password lengths. The five-attempts-per-minute IP limiter substantially reduces practical exploitability.

**Recommendation:** Hash both supplied and configured values to a fixed-length digest before comparing them with `FixedTimeEquals`.

### LOW-06: The Azure AI Services account permits public network access

**Evidence:** `infra/resources.bicep:12-27`

The AI Services account sets `publicNetworkAccess: 'Enabled'` without network ACLs or a private endpoint. `disableLocalAuth: true` and managed-identity RBAC significantly reduce the risk, but the service endpoint remains reachable from arbitrary networks.

**Recommendation:** For stricter environments, use a private endpoint and App Service VNet integration, or restrict access with network ACLs. Retain `disableLocalAuth: true`.

### LOW-07: Production host-header validation is unrestricted

**Evidence:** `web/src/VoiceLive.Web/appsettings.json:7`

`AllowedHosts` is set to `*`. This removes ASP.NET Core host filtering and weakens defense in depth against malformed Host headers and host-based cache or link-generation issues.

**Recommendation:** Set production `AllowedHosts` to the deployed App Service hostname and any explicitly supported custom domains.

### LOW-08: Platform diagnostic logs are not connected to Log Analytics

**Evidence:** `infra/resources.bicep:34-48`, with no `Microsoft.Insights/diagnosticSettings` resources

Application Insights is configured for application telemetry, but App Service platform logs and AI Services audit/resource logs are not explicitly streamed to the Log Analytics workspace. This limits incident investigation and operational troubleshooting.

**Recommendation:** Add diagnostic settings for the Web App and AI Services account, selecting relevant HTTP, console, authentication, audit, and resource log categories with an appropriate retention policy.

### LOW-09: Dynamic connection status is not announced to assistive technology

**Evidence:** `web/frontend/src/views.ts:64-69`, `web/frontend/src/views.ts:139-146`

Routine connection, WebRTC, microphone, turn, speech, and avatar status changes update plain paragraph elements. Screen-reader users are unlikely to be notified of these important transitions, although error and non-fatal banners already use suitable roles.

**Recommendation:** Mark the status container as `role="status"` with `aria-live="polite"` and add an accessibility assertion to the browser tests.

## Confirmed strengths

- Authentication fails closed; unauthenticated API and WebSocket requests return 401.
- Cookies are `HttpOnly`, `SameSite=Lax`, and secure outside development.
- Login requests are rate limited.
- Browser WebSocket origins are checked, and the Azure deployment explicitly configures the allowed HTTPS origin.
- Azure App Service enables forwarded-header processing, preventing the reverse-proxy scheme mismatch that would otherwise affect HTTPS redirects and same-origin checks.
- CSP, frame denial, MIME-sniffing protection, referrer policy, HSTS, HTTPS-only hosting, disabled FTPS, and TLS 1.2 minimum are configured.
- Azure AI local-key authentication is disabled; the Web App uses managed identity with scoped role assignments.
- Azure access tokens and service credentials are not sent to the browser.
- WebSocket messages are capped at 1 MiB, sends are serialized, and session resources are disposed reliably.
- Frontend disconnect and reconnect paths clean up media tracks, audio nodes, peer connections, timers, and sockets.
- Configuration validation is thorough and strongly covered by tests.
- Dependency lock files and reproducible `npm ci` builds are used.

## Prioritized remediation order

1. Make avatar autoplay failure recoverable for display and landing views.
2. Move the shared login password to Key Vault.
3. Add explicit CI security, dependency, and Bicep gates.
4. Enforce idle and absolute session limits.
5. Harden protocol validation and client/server error handling.
6. Address remaining network, observability, audio-quality, and accessibility improvements.
