# Contributing

## Prerequisites

| Tool | Version | Needed for |
|---|---|---|
| .NET SDK | 10.0+ | Server build and tests |
| Node.js | 24 | Frontend build, type check, Playwright |
| Python 3 | any | **The Playwright suite only** — `playwright.config.ts` shells out to `python3 -m http.server`. Tests fail confusingly without it. |
| Azure CLI | latest | Local Azure auth via `DefaultAzureCredential` |
| Azure Developer CLI (`azd`) | latest | Deployment |

You also need an Azure identity holding **Cognitive Services User** (on the Foundry **account**) and **Foundry User** (on the Foundry **project**). Without both, the app starts, `/api/health` reports Healthy, and every session fails with a `403`.

## Setup

```bash
git clone https://github.com/JoranBergfeld/foundry-voice-live-avatar.git
cd foundry-voice-live-avatar

az login

# Local credentials — stored outside the repository, never committed
dotnet user-secrets --project web/src/VoiceLive.Web set "Auth:Username" "<your-username>"
dotnet user-secrets --project web/src/VoiceLive.Web set "Auth:Password" "<your-password>"

dotnet run --no-launch-profile --project web/src/VoiceLive.Web
```

`--no-launch-profile` is required: `launchSettings.json` pins port 5280 and overrides `ASPNETCORE_URLS`, which conflicts with the documented development port. Without the flag the documented URL does not match where the app listens.

The frontend builds automatically as an MSBuild step. Pass `-p:SkipFrontendBuild=true` to skip it when you are only touching server code.

## Tests

```bash
# Backend — no frontend build
dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true

# Frontend type check
npm --prefix web/frontend run typecheck

# Playwright end-to-end — needs Python 3 on PATH
npm --prefix web/frontend test
```

Run the backend tests and the type check before opening a pull request. CI runs both.

## Documentation is tested

`web/tests/VoiceLive.Web.Tests/DocumentationTests.cs` fails the build when documentation drifts from the code. The full set of guards is:

- `Maintained_markdown_has_no_broken_relative_links` — every relative link in maintained markdown resolves to an existing file.
- `Maintained_markdown_publishes_no_credential_literals` — no committed secret or password literal in maintained markdown.
- `Development_settings_carry_no_auth_section` — `appsettings.Development.json` must not contain an `Auth` section.
- `Development_settings_carry_no_voicelive_endpoint` — `appsettings.Development.json` must not contain a `VoiceLive` endpoint.
- `Config_schema_documents_only_voice_types_the_session_builder_supports` — only voice types the code actually builds are listed in the config schema.
- `Agent_config_ships_no_keys_the_code_never_reads` — `config/agent.json` must not contain keys that no code path reads.
- `Config_schema_documents_no_unimplemented_agent_keys` — the config schema must not document agent keys the code never reads.
- `Documented_rbac_roles_match_the_bicep_role_assignments` — RBAC role names and GUIDs in maintained markdown must match `infra/resources.bicep`.
- `Maintained_markdown_does_not_assert_a_working_voice_only_fallback` — no maintained file may claim voice continues when the WebRTC connection fails.
- `Every_docs_image_is_referenced_by_maintained_markdown` — no orphaned image files under `docs/images/`.
- `Maintained_markdown_tables_have_consistent_column_counts` — every GFM table row has the same number of columns (pipe characters in inline code must be escaped as `\|`).

**If a documentation test fails, the documentation is wrong** — or the code changed and the documentation did not. Fix the mismatch; do not weaken the test. Every one of these tests exists because a real defect shipped.

## Conventions

- **Commits** follow [Conventional Commits](https://www.conventionalcommits.org/): `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`.
- **Never commit credentials.** Use `dotnet user-secrets` locally and Key Vault references in Azure.
  - **Known trap:** `infra/resources.bicep` writes `Auth__Password` as a plaintext app setting on every `azd provision`. Any provision after you set a Key Vault reference will overwrite it — re-apply the Key Vault reference and verify sign-in after every provision. See [`docs/production-deployment.md`](docs/production-deployment.md) §2.
- **Update the documentation in the same commit as the behaviour change.** Documentation that describes intended-but-unimplemented behaviour is the specific defect this repository has already had to remediate at length.
- **New security-relevant behaviour** should be reflected in [`docs/threat-model.md`](docs/threat-model.md); new architectural decisions get an ADR in [`docs/adr/`](docs/adr/README.md).

## Where things live

[`docs/README.md`](docs/README.md) indexes the maintained documentation.
