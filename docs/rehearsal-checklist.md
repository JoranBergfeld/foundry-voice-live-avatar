# Rehearsal Checklist

## Day before

- [ ] Confirm the Azure AI Foundry resource is in `swedencentral`.
- [ ] Confirm operator RBAC: `Cognitive Services User` + `Foundry User` on the account scope.
- [ ] Run `az login` on the operator machine and select the target subscription.
- [ ] Review `/config` against [`docs/config-schema.md`](config-schema.md).
- [ ] Finalize `config/grounding/company-direction.md`.
- [ ] Confirm safe questions in `config/agent.json`.
- [ ] Validate config:
  ```bash
  dotnet run --project cli/src/VoiceLive.Cli -- validate --config config
  ```
- [ ] Run the model-mode smoke test:
  ```bash
  dotnet run --project cli/src/VoiceLive.Cli -c Release -- run --config config --mode model --text "Say hello in one short sentence." --seconds 30
  ```

## Event-day setup

- [ ] Confirm `az login` is still valid on the operator machine.
- [ ] Start the web app:
  ```bash
  ConfigDir=/home/jbergfeld/vcs/foundry-voice-live-avatar/config ASPNETCORE_URLS=http://127.0.0.1:5210 dotnet run --no-launch-profile --project web/src/VoiceLive.Web
  ```
- [ ] Check health: `curl -s http://127.0.0.1:5210/api/health`.
- [ ] Check browser-safe config: `curl -s http://127.0.0.1:5210/api/config`.
- [ ] Open operator view: `http://127.0.0.1:5210/?view=operator`.
- [ ] Open display view: `http://127.0.0.1:5210/?view=display`.
- [ ] Grant microphone permission in the operator browser.
- [ ] Click a safe question or use hold-to-talk once to satisfy browser autoplay/user-gesture requirements.
- [ ] Confirm avatar video and audio arrive.
- [ ] Confirm one safe question completes end-to-end with streaming transcript and final response.

## During-show controls

- [ ] Use **Hold to talk** for live operator input.
- [ ] Use safe-question buttons to steer back to approved topics.
- [ ] Use repeat to replay the last completed answer when needed.
- [ ] Use barge-in/interrupt controls if the avatar needs to stop speaking.
- [ ] If an avatar/session error appears, the session has closed; reload/restart the tab and repeat the setup interaction.

## Known limitations to brief stakeholders

- [ ] Each browser tab opens its own `/ws/session`; shared operator+display rooms are future work.
- [ ] Agent mode is pending because `company-direction-avatar` does not yet exist in `proj-default`.
- [ ] CLI live microphone/speaker mode is Windows-only; Linux/WSL should use `--text` or `--audio-file`.
