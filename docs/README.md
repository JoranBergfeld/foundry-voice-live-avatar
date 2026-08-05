# Documentation

Organised by what you are trying to do ([Diátaxis](https://diataxis.fr/)).

Documents fall into three categories:

- **Maintained** — warranted accurate against the current code and covered by the drift tests in `web/tests/VoiceLive.Web.Tests/DocumentationTests.cs`. Everything below except where noted.
- **Point-in-time records** — accurate as of a stated commit, deliberately never updated. Marked as such in the tables below.
- **History** — everything under [`history/`](history/). Not maintained, not tested, unsafe as reference.

## Get started

| Document | Read it when |
|---|---|
| [Project README](../README.md) | First. Why the project exists, non-goals, quickstart, architecture overview. |
| [CONTRIBUTING.md](../CONTRIBUTING.md) | Setting up to develop or running the tests. |

## Do a task

| Document | Read it when |
|---|---|
| [runbook.md](runbook.md) | Provisioning Azure resources and rehearsing. |
| [production-deployment.md](production-deployment.md) | Deploying for a real event: identity, secrets, capacity, cost, alerting, rollback, DR. |
| [rehearsal-checklist.md](rehearsal-checklist.md) | The day before and the hours before showtime. |

## Look something up

| Document | Read it when |
|---|---|
| [config-schema.md](config-schema.md) | You need a config field's type, requiredness, default or validation rule. |
| [wire-protocol.md](wire-protocol.md) | You need the `/ws/session` endpoints, frames or payload shapes. Authoritative. |
| [session-flow.md](session-flow.md) | You need the turn lifecycle, the six status channels, or what a view can do. |
| [../web/README.md](../web/README.md) | You need backend/frontend architecture detail, agent-mode setup, or the browser verification procedure. |

## Understand why

| Document | Read it when |
|---|---|
| [adr/](adr/README.md) | You want the reasoning behind an architectural choice, including rejected alternatives. |
| [threat-model.md](threat-model.md) | You are assessing security posture or changing the deployment shape. |
| [../review-merged.md](../review-merged.md) | You want the merged findings of two independent code reviews. **Point-in-time record of commit `d5110dc`** — it quotes defects that have since been fixed and paths that have since moved. Not maintained. |

## Community and security

| Document | Read it when |
|---|---|
| [SECURITY.md](../SECURITY.md) | Reporting a vulnerability. |
| [CODE_OF_CONDUCT.md](../CODE_OF_CONDUCT.md) | Engaging with the project community. |
| [CHANGELOG.md](../CHANGELOG.md) | Reviewing what changed between versions. |

## History — not maintained

[`history/`](history/) holds the original design specification and the agent plans and specs from the project's construction. They record intent at a point in time and are **not** kept in step with the code. Useful for archaeology, unsafe as reference.

- [history/initial-spec.md](history/initial-spec.md) — the original design specification. Its §1 rationale now lives in the [project README](../README.md#why-this-exists).
- [history/superpowers/](history/superpowers/) — implementation plans and specs from the build.
