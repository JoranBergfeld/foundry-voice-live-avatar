# Security policy

## Reporting a vulnerability

**Do not open a public issue for a security vulnerability.**

Report privately through [GitHub private vulnerability reporting](https://github.com/JoranBergfeld/foundry-voice-live-avatar/security/advisories/new). If that is unavailable, contact the repository owner directly through their GitHub profile.

Please include: what the issue is, how to reproduce it, the impact you believe it has, and the commit you tested. Expect an acknowledgement within a week — this is a small project, not a staffed security programme.

## Supported versions

Only the default branch is supported. There are no released versions and no backported fixes.

## Known issues

Two independent security reviews of commit `d5110dc` are published in [`review-merged.md`](review-merged.md), including Critical-severity findings that are **not yet fixed**. Read [Production readiness](README.md#production-readiness) before deploying anywhere exposed. Reporting an issue already listed there is welcome but will be marked as known.

## Scope

In scope: authentication, authorization, credential handling, the `/ws/session` control protocol, config validation, and the deployment templates in `infra/`.

Out of scope: Azure platform vulnerabilities (report to Microsoft), findings that require an already-compromised operator machine, and issues that depend on ignoring the documented deployment constraints — the app is **not internet-facing by intent, but `azd up` publishes a public App Service with no IP restrictions or VNet integration**. Nothing enforces that boundary; restricting network access is the operator's responsibility. See [Non-goals](README.md#non-goals) and `docs/adr/0003-shared-cookie-authentication.md`.
