# Agent Session Configuration Ownership Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the hosted Voice Live agent the sole owner of Voice Live session configuration in agent mode.

**Architecture:** Centralize mode-sensitive option selection in `SessionOptionsBuilder`: model mode returns configured options and agent mode returns no options. `VoiceLiveSessionFactory` calls `ConfigureSessionAsync` only when options exist, while preserving disposal after model configuration failures.

**Tech Stack:** C# 14, .NET 10, Azure.AI.VoiceLive 1.1.0, xUnit

---

### Task 1: Stop local agent session updates

**Files:**
- Modify: `web/tests/VoiceLive.Web.Tests/ServerSessionConfigTests.cs`
- Modify: `web/src/VoiceLive.Web/Session/SessionOptionsBuilder.cs`
- Modify: `web/src/VoiceLive.Web/Session/VoiceLiveSessionFactory.cs`

- [ ] **Step 1: Write the failing mode-selection test**

Replace the old `BuildForAgent` test with a test asserting `BuildForMode` returns `null` for agent mode, plus a model-mode assertion that retains the configured model and instructions.

- [ ] **Step 2: Verify the new test fails**

Run:

```bash
dotnet test web/tests/VoiceLive.Web.Tests/VoiceLive.Web.Tests.csproj --no-restore -p:SkipFrontendBuild=true --filter FullyQualifiedName~BuildForMode
```

Expected: compilation fails because `SessionOptionsBuilder.BuildForMode` does not exist.

- [ ] **Step 3: Implement mode-sensitive option selection**

Add `BuildForMode(ServerSessionConfig config, string instructions)` returning `null` for agent mode and existing model options otherwise. Remove `BuildForAgent`, and update `VoiceLiveSessionFactory` to call `ConfigureSessionAsync` only for a non-null result.

- [ ] **Step 4: Verify the focused tests pass**

Run the Step 2 command again. Expected: both `BuildForMode` tests pass.

### Task 2: Align maintained documentation

**Files:**
- Modify: `web/README.md`
- Modify: `docs/runbook.md`
- Modify: `docs/config-schema.md`

- [ ] **Step 1: Document authoritative ownership**

State that agent mode sends no local Voice Live session update and that hosted agent configuration owns voice, avatar, audio, and turn detection as well as model, instructions, and tools. Clarify that local session and avatar files configure model mode.

- [ ] **Step 2: Run documentation tests**

```bash
dotnet test web/tests/VoiceLive.Web.Tests/VoiceLive.Web.Tests.csproj --no-restore -p:SkipFrontendBuild=true --filter FullyQualifiedName~DocumentationTests
```

Expected: all documentation tests pass.

### Task 3: Full verification

**Files:**
- Verify all modified files

- [ ] **Step 1: Run the complete web test suite**

```bash
dotnet test web/VoiceLive.Web.sln --no-restore -p:SkipFrontendBuild=true
```

Expected: all tests pass with zero failures.

- [ ] **Step 2: Inspect the final diff**

Confirm agent mode contains no `ConfigureSessionAsync` path and no `BuildForAgent` API, model mode remains configured locally, and unrelated refactor changes are preserved.