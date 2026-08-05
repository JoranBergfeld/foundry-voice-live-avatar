# Documentation Alignment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every claim in this repository's documentation true, add the missing production-deployment and rationale documentation, and install automated drift tests so the documentation cannot silently diverge from the code again.

**Architecture:** Three layers, executed in order. **Layer 1 (Tasks 1–4)** adds executable documentation-drift tests to the existing xUnit project — these fail first and define "done" for the content work. **Layer 2 (Tasks 5–11)** corrects the six verified false claims and closes the credential-publication gap. **Layer 3 (Tasks 12–20)** adds the missing documents (why/non-goals, production deployment, wire-protocol reference, ADRs, threat model, community-health files) and restructures `docs/` so maintained references are distinguishable from historical agent process output.

**Tech Stack:** Markdown, xUnit + .NET 10 (`web/tests/VoiceLive.Web.Tests`), `dotnet user-secrets`, GitHub community-health file conventions, Diátaxis documentation structure.

**Source spec:** [`review-merged.md`](../../../review-merged.md) Part B, findings **D-01 … D-24**.

---

## Scope

**In scope.** All documentation content and structure. Plus three minimal non-documentation changes required to stop the docs from lying: adding `UserSecretsId` to the web project, removing the `Auth` section from `appsettings.Development.json`, and deleting three dead keys from `config/agent.json`. Each is called out where it appears.

**Out of scope.** Every code finding in `review-merged.md` Part A (C-01, C-02 code half, H-01…H-05, M-01…M-15, L-01…L-20). Those belong to a separate implementation plan. Where a documentation fix describes current behaviour that a Part A fix will later change, the task says so explicitly and names the finding.

**Deliberate ordering constraint.** Documentation is corrected to describe **behaviour that exists today**, not behaviour that is planned. Task 6 (autoplay) and Task 5 (reconnect) will both need a one-line revisit when H-05 and L-06 land. That is correct: docs that describe the future are the defect this plan exists to remove.

---

## Commit convention

Every commit in this plan uses Conventional Commits and appends these trailers:

```
Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
Copilot-Session: 74a61d1f-17e7-42cc-8135-7e78c446a579
```

To avoid repeating them 20 times, each commit step below shows only the subject line. Append the trailers to every commit.

---

## File Structure

### Created

| Path | Responsibility |
|---|---|
| `web/tests/VoiceLive.Web.Tests/DocumentationTests.cs` | All documentation-drift tests. One file: these tests share the repo-root helper and the maintained-docs file set, and they change together. |
| `docs/README.md` | Index of maintained documentation; separates current reference from historical process output. |
| `docs/production-deployment.md` | The missing production guide: identity, secrets, capacity, cost, networking, observability, environments, rollback, DR, data handling. |
| `docs/wire-protocol.md` | Authoritative `/ws/session` frame reference — the single source of truth that `README.md` and `web/README.md` will link to instead of restating. |
| `docs/session-flow.md` | Turn lifecycle, connection-state model, and per-view journeys. Home for the three orphaned diagrams. |
| `docs/threat-model.md` | Actors, assets, entry points, trust assumptions, accepted risks. |
| `docs/adr/README.md` | ADR index and format note. |
| `docs/adr/0001-server-side-credential-custody.md` | Why the browser never holds an Azure credential. |
| `docs/adr/0002-direct-webrtc-media-plane.md` | Why avatar media bypasses the server. |
| `docs/adr/0003-shared-cookie-authentication.md` | Why one shared credential, and the limits that follow. |
| `docs/adr/0004-startup-only-config-validation.md` | Why config is validated once and invalid config means unhealthy-but-running. |
| `docs/adr/0005-per-instance-session-cap.md` | Why `MaxConcurrentSessions = 2` and why the gate is per-instance. |
| `docs/adr/0006-region-pinned-swedencentral.md` | Why the region is pinned. |
| `SECURITY.md` | Vulnerability reporting channel. |
| `CONTRIBUTING.md` | Development setup, full prerequisites, every test command. |
| `CODE_OF_CONDUCT.md` | Contributor Covenant. |
| `CHANGELOG.md` | Keep a Changelog format. |

### Modified

| Path | Change |
|---|---|
| `README.md` | Add why/non-goals and production-readiness warning; fix reconnect and CSP claims; remove published credentials; add Development section; link to new docs instead of restating the wire protocol. |
| `web/README.md` | Remove published credentials and the hardcoded absolute path; delegate the endpoint/frame tables to `docs/wire-protocol.md`. |
| `docs/runbook.md` | Fix the autoplay claim; remove published credentials; remove point-in-time test evidence; reconcile RBAC role names; link to `docs/production-deployment.md`. |
| `docs/config-schema.md` | Remove `azure-custom`; remove the three unimplemented `agent.json` keys; remove the published password default. |
| `docs/rehearsal-checklist.md` | Reconcile RBAC role names. |
| `docs/initial-spec.md` | Add a historical-status banner. |
| `config/agent.json` | Delete `agentVersion`, `conversationResumePolicy`, `groundingStrategy` — nothing reads them. |
| `web/src/VoiceLive.Web/appsettings.Development.json` | Delete the `Auth` section. |
| `web/src/VoiceLive.Web/VoiceLive.Web.csproj` | Add `UserSecretsId`. |
| `licence.md` → `LICENSE.md` | Rename so GitHub, `dotnet pack` and SBOM tooling detect it. |

### Renamed / relocated

| From | To |
|---|---|
| `docs/superpowers/` | `docs/history/superpowers/` |
| `licence.md` | `LICENSE.md` |

---

## Verification commands used throughout

```bash
# Backend tests (frontend build skipped, as CI does)
dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true

# Just the documentation tests
dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true --filter "FullyQualifiedName~DocumentationTests"
```

### Expect the link guard to be red in the middle of this plan

From Task 10 onward, several tasks add links to documents that later tasks create. This is intentional — the guard tracks the debt, and each forward reference is closed by a named task. Do **not** delete a link to make the test green.

| Forward reference | Added by | Resolved by |
|---|---|---|
| `CONTRIBUTING.md` | Task 10, Task 14 | Task 20 |
| `docs/adr/0003-shared-cookie-authentication.md` | Task 10 | Task 18 |
| `docs/production-deployment.md` | Task 13 | Task 15 |
| `docs/adr/0006-region-pinned-swedencentral.md` | Task 15 | Task 18 |
| `docs/README.md` | Task 12, Task 20 | Task 21 |

`Maintained_markdown_has_no_broken_relative_links` is expected to pass at Task 1, go red at Task 10, and return to green at Task 21. Every other test goes red-then-green within its own task.

---

## Task 1: Documentation test harness and link integrity

Establishes the shared helpers every later test uses, plus the first guard: relative links in maintained docs must resolve. This guard is what makes the Task 18 restructure safe.

**Files:**
- Create: `web/tests/VoiceLive.Web.Tests/DocumentationTests.cs`

- [ ] **Step 1: Write the harness and the failing test**

Create `web/tests/VoiceLive.Web.Tests/DocumentationTests.cs`:

```csharp
using System.Text.RegularExpressions;

public sealed class DocumentationTests
{
    /// <summary>
    /// Repo root, resolved the same way <see cref="TestAppFactory.RepoConfigDir"/> resolves the config
    /// directory: six levels up from bin/&lt;cfg&gt;/net10.0 lands on the repository root.
    /// </summary>
    internal static string RepoRoot
    {
        get
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
            Assert.True(
                File.Exists(Path.Combine(root, "README.md")),
                $"Repo root resolution failed: no README.md at '{root}'. Fix the relative depth in DocumentationTests.RepoRoot.");
            return root;
        }
    }

    /// <summary>
    /// Markdown that the project maintains and warrants as accurate. Excludes historical agent
    /// process output and the standalone review artifacts, which are point-in-time records.
    /// </summary>
    internal static IEnumerable<string> MaintainedMarkdown()
    {
        var root = RepoRoot;
        return Directory
            .EnumerateFiles(root, "*.md", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
            .Where(rel => !rel.StartsWith("docs/superpowers/", StringComparison.Ordinal))
            .Where(rel => !rel.StartsWith("docs/history/", StringComparison.Ordinal))
            .Where(rel => !rel.Contains("/bin/", StringComparison.Ordinal))
            .Where(rel => !rel.Contains("/obj/", StringComparison.Ordinal))
            .Where(rel => !rel.Contains("node_modules/", StringComparison.Ordinal))
            // Review artifacts quote the very defects these tests detect (credential literals,
            // `azure-custom`, dead config keys). They are point-in-time records, not warranties.
            .Where(rel => !Regex.IsMatch(Path.GetFileNameWithoutExtension(rel), @"(^|[-_])review([-_]|$)", RegexOptions.IgnoreCase))
            .OrderBy(rel => rel, StringComparer.Ordinal)
            .ToList();
    }

    // Fenced code block: optional indentation, 3+ backticks or tildes, optional info string, any content, closing fence.
    // The opening and closing fence character and count must match.
    // Handled separately for backticks vs tildes; SINGLELINE makes . match newlines.
    private static readonly Regex FencedCodeBlock = new(
        @"^(?'ind'[ \t]*)(?'fence'(?:`{3,}|~{3,}))[ \t]*\S*[ \t]*\r?\n.*?\r?\n\k'ind'\k'fence'[ \t]*(?:\r?\n|$)",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.Singleline);

    private static readonly Regex InlineCode = new(@"`[^`\r\n]+`", RegexOptions.Compiled);

    // Allows one level of balanced parentheses in the destination (e.g., ./path(1)/file.md).
    private static readonly Regex MarkdownLink = new(
        @"\]\(([^()\s]+(?:\([^()]*\)[^()\s]*)*)\)",
        RegexOptions.Compiled);

    /// <summary>Removes fenced code blocks and inline code spans so that link-like syntax inside
    /// code is not mistaken for real links.</summary>
    private static string StripCodeFromMarkdown(string text)
    {
        // Strip longer fences first so a 4-backtick fence containing 3-backtick fences is removed
        // as one unit before the inner fences could be matched.
        var result = FencedCodeBlock.Replace(text, string.Empty);
        result = InlineCode.Replace(result, string.Empty);
        return result;
    }

    [Fact]
    public void Maintained_markdown_has_no_broken_relative_links()
    {
        var root = RepoRoot;
        var broken = new List<string>();

        foreach (var rel in MaintainedMarkdown())
        {
            var dir = Path.GetDirectoryName(Path.Combine(root, rel))!;
            var prose = StripCodeFromMarkdown(File.ReadAllText(Path.Combine(root, rel)));
            foreach (Match match in MarkdownLink.Matches(prose))
            {
                var target = match.Groups[1].Value.Trim();
                if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) continue;
                if (target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) continue;
                if (target.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) continue;
                if (target.StartsWith('#')) continue;

                var path = target.Split('#')[0];
                if (path.Length == 0) continue;

                if (!File.Exists(Path.Combine(dir, path)) && !Directory.Exists(Path.Combine(dir, path)))
                    broken.Add($"{rel} -> {target}");
            }
        }

        Assert.True(broken.Count == 0, "Broken relative links:\n  " + string.Join("\n  ", broken));
    }
}
```

- [ ] **Step 2: Run the test**

Run: `dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true --filter "FullyQualifiedName~Maintained_markdown_has_no_broken_relative_links"`

Expected: **PASS**. This is a guard test — maintained docs currently link correctly, and the test exists to keep that true through Tasks 12–20, which add and move many files. If it fails, the repo-root depth in `RepoRoot` is wrong for your build configuration; the assertion message names the resolved path.

- [ ] **Step 3: Commit**

```bash
git add web/tests/VoiceLive.Web.Tests/DocumentationTests.cs
git commit -m "test: add documentation link-integrity guard"
```

---

## Task 2: Fail on published credentials

The precise signal is a **code-formatted** password literal. The word `rehearsal` appears legitimately as prose in several docs ("before rehearsal", "rehearsal checklist"), so the test matches only the backticked form, which yields zero false positives and catches all five real occurrences.

**Files:**
- Modify: `web/tests/VoiceLive.Web.Tests/DocumentationTests.cs`

- [ ] **Step 1: Write the failing test**

Append inside the `DocumentationTests` class, before the closing brace:

```csharp
    /// <summary>
    /// Credential literals that must never appear as code-formatted values in maintained
    /// documentation. Add to this list whenever a credential is retired, never remove from it.
    /// </summary>
    private static readonly string[] ForbiddenCredentialLiterals = ["`rehearsal`"];

    [Fact]
    public void Maintained_markdown_publishes_no_credential_literals()
    {
        var root = RepoRoot;
        var violations = new List<string>();

        foreach (var rel in MaintainedMarkdown())
        {
            var lines = File.ReadAllLines(Path.Combine(root, rel));
            for (var i = 0; i < lines.Length; i++)
            {
                foreach (var literal in ForbiddenCredentialLiterals)
                {
                    if (lines[i].Contains(literal, StringComparison.Ordinal))
                        violations.Add($"{rel}:{i + 1} contains {literal}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Documentation must not publish working credentials. Use `dotnet user-secrets` instructions instead:\n  "
                + string.Join("\n  ", violations));
    }

    [Fact]
    public void Development_settings_carry_no_auth_section()
    {
        var path = Path.Combine(RepoRoot, "web", "src", "VoiceLive.Web", "appsettings.Development.json");
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));

        Assert.False(
            doc.RootElement.TryGetProperty("Auth", out _),
            "appsettings.Development.json must not contain an Auth section. Use `dotnet user-secrets` so credentials are never committed.");
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true --filter "FullyQualifiedName~DocumentationTests"`

Expected: **2 FAIL**.
- `Maintained_markdown_publishes_no_credential_literals` fails listing 5 violations: `README.md:82`, `README.md:243`, `web/README.md:33`, `docs/runbook.md:98`, `docs/config-schema.md:13`.
- `Development_settings_carry_no_auth_section` fails with "must not contain an Auth section".

Both are fixed in Task 10.

- [ ] **Step 3: Commit**

```bash
git add web/tests/VoiceLive.Web.Tests/DocumentationTests.cs
git commit -m "test: fail when docs publish credential literals"
```

---

## Task 3: Fail on config-schema drift

Two drift tests. The first is the exact test Opus recommended for **H-03**: the documented `voice.type` values and the values the code accepts must be one set. The second catches **D-03**: every key shipped in `config/agent.json` must be a property the code actually reads.

**Files:**
- Modify: `web/tests/VoiceLive.Web.Tests/DocumentationTests.cs`

- [ ] **Step 1: Write the failing tests**

Append inside the `DocumentationTests` class:

```csharp
    private static string ConfigSchema() => File.ReadAllText(Path.Combine(RepoRoot, "docs", "config-schema.md"));

    // Keys with zero references anywhere in the codebase. Shipping or documenting them implies
    // behaviour that does not exist (review-merged.md H-04 / D-03).
    // Must be updated in sync with any future implementation work.
    private static readonly string[] UnimplementedAgentKeys =
        ["agentVersion", "conversationResumePolicy", "groundingStrategy"];

    [Fact]
    public void Config_schema_documents_only_voice_types_the_session_builder_supports()
    {
        // Values the app can actually build a session with.
        // Source of truth 1 (accepted set): web/src/VoiceLive.Web/Config/WebConfig.cs line 22
        //   private static readonly string[] VoiceTypes = ["azure-realtime-native", "azure-standard", "azure-custom", "openai"];
        // Source of truth 2 (buildable set): web/src/VoiceLive.Web/Session/SessionOptionsBuilder.cs lines 51-56
        //   "azure-custom" throws; the other three build successfully.
        // Both sources are private so they cannot be referenced directly here. Keep these lists
        // in sync whenever WebConfig.cs or SessionOptionsBuilder.cs change.
        string[] buildable = ["azure-realtime-native", "azure-standard", "openai"];

        var schema = ConfigSchema();

        foreach (var value in buildable)
            Assert.True(schema.Contains($"`{value}`", StringComparison.Ordinal),
                $"docs/config-schema.md must document supported voice type '{value}'.");

        // `azure-custom` passes WebConfig startup validation but SessionOptionsBuilder always
        // throws on it (no custom-voice endpoint id configured). It must not be documented as
        // an allowed value in any format — bullet list, table row, inline list, or prose.
        // The ONE permitted mention is on a line whose trimmed text starts with exactly
        // "- **Known trap:**" (the approved Task 8 warning line). That shape is narrow enough
        // that it cannot be confused with an allowed-values table cell or bullet, and appending
        // the marker to an existing line does not satisfy it.
        var schemaLines = schema.Split('\n');
        var badLines = schemaLines
            .Select((line, i) => (line, i))
            .Where(x => x.line.Contains("`azure-custom`", StringComparison.Ordinal)
                        && !x.line.TrimStart().StartsWith("- **Known trap:**", StringComparison.Ordinal))
            .Select(x => $"line {x.i + 1}: {x.line.Trim()}")
            .ToList();

        Assert.True(badLines.Count == 0,
            "`azure-custom` is accepted by startup validation but always throws in SessionOptionsBuilder " +
            "(no custom-voice endpoint id configured), so it must not be documented as an allowed voice.type. " +
            "Remove it from every allowed-values list (table cells, bullets, inline enumerations). " +
            "Offending lines:\n  " + string.Join("\n  ", badLines) +
            "\nSee review-merged.md H-03 / D-04.");
    }

    [Fact]
    public void Agent_config_ships_no_keys_the_code_never_reads()
    {
        var path = Path.Combine(RepoRoot, "config", "agent.json");
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));

        var present = UnimplementedAgentKeys
            .Where(key => doc.RootElement.TryGetProperty(key, out _))
            .ToList();

        Assert.True(present.Count == 0,
            "config/agent.json ships keys that no code reads: " + string.Join(", ", present) +
            ". Remove them, or implement and validate them.");
    }

    [Fact]
    public void Config_schema_documents_no_unimplemented_agent_keys()
    {
        var schema = ConfigSchema();

        var documented = UnimplementedAgentKeys
            .Where(key => schema.Contains($"`{key}`", StringComparison.Ordinal))
            .ToList();

        Assert.True(documented.Count == 0,
            "docs/config-schema.md documents agent.json keys that no code reads: " + string.Join(", ", documented) +
            ". The schema also promises they 'fail fast at startup', which is false.");
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true --filter "FullyQualifiedName~DocumentationTests"`

Expected: **3 new FAIL** (5 failing total with Task 2's).
- `Config_schema_documents_only_voice_types_the_session_builder_supports` — `azure-custom` is listed.
- `Agent_config_ships_no_keys_the_code_never_reads` — all three keys present.
- `Config_schema_documents_no_unimplemented_agent_keys` — all three documented.

Fixed in Tasks 7 and 8.

- [ ] **Step 3: Commit**

```bash
git add web/tests/VoiceLive.Web.Tests/DocumentationTests.cs
git commit -m "test: fail on config-schema and agent.json drift"
```

---

## Task 4: Fail on orphaned documentation images

**Files:**
- Modify: `web/tests/VoiceLive.Web.Tests/DocumentationTests.cs`

- [ ] **Step 1: Write the failing test**

Append inside the `DocumentationTests` class:

```csharp
    [Fact]
    public void Every_docs_image_is_referenced_by_maintained_markdown()
    {
        var root = RepoRoot;
        var imagesDir = Path.Combine(root, "docs", "images");
        if (!Directory.Exists(imagesDir)) return;

        var corpus = string.Concat(MaintainedMarkdown().Select(rel => File.ReadAllText(Path.Combine(root, rel))));

        // Known limitation: two images with the same basename in different subdirectories are
        // indistinguishable to the matcher; accepted because today all images are in one flat directory.
        string[] imageExtensions = [".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp"];
        var orphans = Directory
            .EnumerateFiles(imagesDir, "*", SearchOption.AllDirectories)
            .Where(f => imageExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Where(f => !corpus.Contains(Path.GetFileName(f), StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .ToList();

        Assert.True(orphans.Count == 0,
            "Unreferenced images in docs/images — wire them into a document or delete them:\n  "
                + string.Join("\n  ", orphans));
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true --filter "FullyQualifiedName~Every_docs_image_is_referenced"`

Expected: **FAIL** listing three orphans as repo-relative paths: `docs/images/voice_live_decision_points.png`, `docs/images/voice_live_prewarm_connection_flow.png`, `docs/images/voice_live_single_turn_flow.png`. Fixed in Task 15.

- [ ] **Step 3: Commit**

```bash
git add web/tests/VoiceLive.Web.Tests/DocumentationTests.cs
git commit -m "test: fail on unreferenced docs images"
```

---

## Task 5: D-01 — correct the "automatic reconnect" claim

`README.md` claims automatic reconnect twice. `views.ts` creates a manual `Reconnect` button in all three views and there is no retry timer or backoff anywhere in the frontend. Fix the documentation to match today's behaviour. (Code finding **L-06** may later add real backoff; this line then gets updated.)

**Files:**
- Modify: `README.md` (2 locations)

- [ ] **Step 1: Fix the overview claim**

In `README.md`, replace:

```markdown
By default the app runs in **model mode** using `gpt-realtime`. Optional **agent mode** uses a named Voice Live agent created in the Azure AI Foundry portal. Reliability features include manual turn gating (Hold to talk, gated, or open-mic), automatic reconnect, health and error reporting at `/api/health`, safe-question injection, and voice-only fallback when avatar capacity is unavailable.
```

with:

```markdown
By default the app runs in **model mode** using `gpt-realtime`. Optional **agent mode** uses a named Voice Live agent created in the Azure AI Foundry portal. Reliability features include manual turn gating (Hold to talk, gated, or open-mic), an operator-initiated **Reconnect** control on every view, health and error reporting at `/api/health`, safe-question injection, and voice-only fallback when avatar capacity is unavailable.
```

- [ ] **Step 2: Fix the architecture claim**

In `README.md`, replace:

```markdown
- **Error and reconnect** — transient errors trigger automatic reconnect with backoff; fatal errors surface in the operator view.
```

with:

```markdown
- **Error and reconnect** — reconnection is **operator-initiated, not automatic**. On disconnect every view reveals a **Reconnect** button; there is no retry timer and no backoff. Fatal errors surface as an error banner; non-fatal avatar errors surface as a separate notice while voice continues. An unattended `?view=display` screen will therefore stay disconnected until someone clicks Reconnect — staff accordingly.
```

- [ ] **Step 3: Verify no stale claim remains**

Run: `grep -rn "automatic reconnect\|automatically reconnect\|reconnect with backoff" README.md web/README.md docs/*.md`

Expected: **no output.**

- [ ] **Step 4: Commit**

```bash
git add README.md
git commit -m "docs: correct reconnect claim to match manual Reconnect control"
```

---

## Task 6: D-02 — correct the autoplay claim

`docs/runbook.md` §7 says a blocked autoplay shows a banner asking the operator to interact. In `main.ts` any non-`AbortError` rejection from `play()` calls `disconnect()`, ending the whole Voice Live session. The runbook and `docs/rehearsal-checklist.md` currently contradict each other. (Code finding **H-05** will make this recoverable; this section then gets updated.)

**Files:**
- Modify: `docs/runbook.md` (§7 Avatar operation, §9 Failure handling, §10 Troubleshooting)
- Modify: `docs/rehearsal-checklist.md` (During-show controls, Known limitations)

- [x] **Step 1: Replace the autoplay paragraph** (`docs/runbook.md` §7)

Replaced the old paragraph (claiming a non-fatal banner) with three paragraphs that: (a) stress pre-arrival interaction, (b) name the `NotAllowedError` → `disconnect()` path and show the fatal banner text, and (c) call out the unattended display hazard and H-05. Recovery sentence adjusted from the spec's original ("reload the tab, interact with the page, and reconnect") to "click Reconnect (the click satisfies the browser gesture); reload only if Reconnect fails" — clicking Reconnect is itself a user gesture so a full reload is not required as the first step.

- [x] **Step 2: Verify the two documents now agree**

Run: `grep -n "autoplay\|session has closed" docs/runbook.md docs/rehearsal-checklist.md`

Expected: both files describe session termination on blocked autoplay; neither claims a non-fatal banner.

- [x] **Step 3 (carry-forward B1): Add Reconnect control to `docs/runbook.md` §9 Failure handling**

Added a bullet after the error-banner bullet noting that a Reconnect button appears on every fatal disconnect, clicking it re-runs `start()` without a reload (preserving sign-in, mic grant, autoplay gesture), and is the first recovery action for a closed session.

- [x] **Step 4 (carry-forward B2): Correct `docs/runbook.md` §10 Troubleshooting row**

"No avatar video/audio in browser" — changed "reload the tab if the session closed" to "click Reconnect first; reload only if Reconnect fails".

- [x] **Step 5 (carry-forward B3): Correct `docs/rehearsal-checklist.md` During-show controls**

Changed "reload/restart the tab and repeat the setup interaction" to "click Reconnect to restore it; reload only if Reconnect fails".

- [x] **Step 6 (carry-forward B4): Add unattended-display hazard to `docs/rehearsal-checklist.md` Known limitations**

Added a new bullet: a `?view=display` screen that disconnects stays dead until a human clicks Reconnect; ensure a human is present to respond.

**Code-review corrections (post-merge, Task 6 addendum):**

- [x] **Fix HIGH — conflated failure modes (rehearsal-checklist.md:40):** Split the single "avatar/session error" bullet in During-show controls into two separate bullets keyed to visible on-screen discriminators: (a) fatal error banner + Reconnect button visible → click Reconnect, reload only if that fails; (b) non-fatal "Avatar unavailable" notice + no Reconnect button → do NOT reconnect or reload, continue without avatar video. The old wording incorrectly implied the operator should reload on a non-fatal avatar error, which would destroy a working voice session mid-show.

- [x] **Fix MEDIUM — display-tab gesture step missing (rehearsal-checklist.md):** Added a new pre-show step immediately after opening the display view: click anywhere on the display screen once and confirm avatar video and audio arrive in that tab specifically. Scoped the existing operator-tab gesture step explicitly to the operator tab (the two controls referenced — safe-question buttons and hold-to-talk — only exist in the operator view). User activation is per-document; a gesture in the operator tab does not satisfy the display tab's autoplay requirement.

- [x] **Fix HIGH (Finding A) — display-tab gesture step placed before sign-in (rehearsal-checklist.md:28):** The display-tab click/confirm step was above the "Sign in" step, making it unsatisfiable: `/?view=display` is auth-gated and redirects to `/login` for unauthenticated requests (confirmed by `AuthTests.Root_without_cookie_redirects_to_login`). Moved the display-tab gesture/confirm step to after sign-in. Added an explicit instruction to sign in in the display tab (on separate machine/browser) or reload after signing in (same profile). Made the separate-machine case explicit, as that is the normal venue setup.

- [x] **Fix HIGH (Finding B) — "voice session still active, continue the show" is false (repo-wide):** `main.ts` puts video and audio recvonly transceivers on the same peer connection; `handleAvatarError` calls `this.pc?.close()`, destroying the only inbound media path. `VoiceLiveWebSocketBridge.cs` handles no `response.audio.delta` case and all outbound sends are `WebSocketMessageType.Text`; non-string frames are dropped by `main.ts:119`. When avatar fails, **both audio and video are lost — there is no voice-only fallback**. Corrected everywhere the false claim appeared:
  - `README.md:13` — removed "voice-only fallback" from feature list
  - `README.md:94` — replaced "session continues in voice-only mode" with accurate description
  - `README.md:263` (failure table) — updated row to state audio is also lost
  - `docs/runbook.md` §9 — replaced "keeps voice session running" with accurate description; added tracked finding note (H-07) linking to `review-merged.md` noting that forwarding `response.audio.delta` would make voice-only fallback real
  - `docs/runbook.md` §10 (troubleshooting table) — updated `avatar_service_resource_exhausted` row to remove "Voice continues to work without avatar"
  - `docs/rehearsal-checklist.md` — replaced "continue the show" with accurate fallback instruction
  - **Note:** `main.ts:412` has a stale comment; it is out of scope (code change), flagged in report.

**Post-merge HIGH findings (code-review task, recorded here):**

- [x] **Fix HIGH — fabricated finding ID H-07 (`docs/runbook.md:141`):** Removed the "Tracked as finding H-07 in `review-merged.md`" reference (no such ID exists; H series stops at H-05). Replaced with: *"This is a known gap, not a design decision; forwarding `response.audio.delta` to the browser and playing it would make true voice-only fallback possible, at which point this section must be updated."* The H-05 citation at `runbook.md:118` was verified correct and left untouched.

- [x] **Fix HIGH — three surviving "voice continues" falsehoods:** `README.md:225` (event table), `README.md:238` (Error and reconnect bullet), and `web/README.md:23` (WebSocket event list) all still asserted that voice continues after an avatar error. Corrected all three to state that avatar video **and audio** are both lost (both ride the same WebRTC peer connection) while WebSocket, microphone capture, and transcripts survive but there is no audible output to the room.

- [x] **Fix HIGH — display-tab sign-in step unsatisfiable (`docs/rehearsal-checklist.md:30`):** `Program.cs:106` redirects unauthenticated requests to `/login` with no `ReturnUrl`, discarding `?view=display`; `LoginEndpoints.cs:30` redirects POST to `/`, not `/?view=display`. Both same-profile and separate-machine paths left the operator on the wrong URL with no route to the display view without explicit re-navigation. Fixed by replacing the "reload" instruction with an explicit *"navigate the display tab to `/?view=display`"* step after sign-in, with an explanatory parenthetical. The gesture step (click in display tab) and confirm step remain in the correct order: sign in → navigate to `/?view=display` → click once → confirm video and audio.

- [x] **Fix HIGH — "confirm audio in display tab" is unsatisfiable (`docs/rehearsal-checklist.md:32`):** The display tab opens its own independent `/ws/session` that receives no microphone input and has no operator controls; its avatar sits idle and silent forever. Video arrives; speech never does. Changed the confirm step to ask only for what is actually observable: avatar video present, connection status healthy, and avatar **idle and silent** as the expected correct state. Added explicit statement that room audio must come from the operator machine. Connected the per-tab-session notes to this consequence in the "Known limitations" section (`docs/rehearsal-checklist.md`), `docs/runbook.md:108`, and `web/README.md:61`.

**Post-review documentation defects (pre-existing on main, fixed in this round):**

- [x] **Fix HIGH — rehearsal checklist opens three tabs against a cap of two (`docs/rehearsal-checklist.md`):** The Event-day setup sequence instructed opening `/` (landing), `/?view=operator`, and `/?view=display`. Every view starts a session via `boot()` → `client.start()`; `SessionGate.TryEnter()` is a non-blocking `Wait(0)` that rejects a third session immediately. With the default `MaxConcurrentSessions: 2` (set in `appsettings.json` and defaulted in `VoiceLiveOptions.cs`), the third tab shows the fatal startup error frame. Fixed by: (1) replacing the landing-tab open step with a direct `/?view=operator` step plus an inline warning to close `/` if accidentally opened; (2) adding a note on the display-tab step that two slots are now consumed and no third tab may be opened; (3) adding a Known Limitations bullet stating the default cap of 2, that a third tab is rejected immediately, and that `MaxConcurrentSessions` is tunable. Also added the same cap note to `docs/runbook.md` §6 where it listed all three views without mentioning the limit.

- [x] **Fix HIGH — operator-tab sign-in and navigate defect (`docs/rehearsal-checklist.md` Event-day setup):** The checklist opened `/?view=operator` and `/?view=display` *before* sign-in. `Program.cs:106` redirects unauthenticated requests to `/login` with no `ReturnUrl`, silently discarding the `?view` query string; `LoginEndpoints.cs:30` POSTs back to `/` after sign-in. So: (a) both pre-sign-in view steps landed on `/login` with the view discarded; (b) the sign-in step then left the operator on the landing `/`, which loaded `app.js` and called `client.start()`, consuming a session slot — exactly what the now-removed step-1 note warned against; (c) no step told the operator to navigate to `/?view=operator` after signing in. The display-tab path had received a prior fix; the operator path had not. Fixed by: restructuring the Event-day setup to authenticate first (explicit `/login` URL), then navigate the landing tab to `/?view=operator` (with an explanation that `beforeunload` → `client.dispose()` releases the landing session before the operator view claims one, so the count remains ≤ 2 transiently), then open the display tab. The session-cap note (`MaxConcurrentSessions`, default 2) is retained on the operator-navigate step.

- [x] **Fix MEDIUM — unescaped pipe in GFM table drops Operator action column (`docs/runbook.md:155`):** `` `DOTNETCORE|10.0` `` inside a table cell split the row into 4 cells in a 3-column table, causing GFM parsers to discard the remedy column entirely. Escaped as `DOTNETCORE\|10.0`. Inline occurrence at `runbook.md:47` is outside a table and left unchanged. Swept all maintained markdown tables for consistent cell counts — no further defects found.

---

## Task 7: D-03 — remove the three unimplemented `agent.json` keys

`agentVersion`, `conversationResumePolicy` and `groundingStrategy` have **zero** references in any `.cs` or `.ts` file. `docs/config-schema.md` marks two of them Required and promises they "fail fast at startup". Remove them from both the shipped config and the schema. This makes two Task 3 tests pass.

**Files:**
- Modify: `config/agent.json`
- Modify: `docs/config-schema.md`

- [ ] **Step 1: Confirm they are genuinely unreferenced**

Run: `grep -rn "groundingStrategy\|GroundingStrategy\|conversationResumePolicy\|ConversationResumePolicy\|agentVersion\|AgentVersion" --include='*.cs' --include='*.ts' web/`

Expected: **no output.** If there is output, stop — the premise is wrong and this task must be redesigned.

- [ ] **Step 2: Remove the keys from the shipped config**

Edit `config/agent.json` and delete the `agentVersion`, `conversationResumePolicy` and `groundingStrategy` properties. The file must keep `agentName`, `agentProjectName` and `safeQuestions`, and must remain valid JSON — check for a trailing comma on the last remaining property.

- [ ] **Step 3: Remove the rows from the schema table**

In `docs/config-schema.md`, delete these three rows from the `agent.json` table:

```markdown
| `agentVersion` | string or null | Optional | Default: `null` | Optional pinned agent version. |
| `conversationResumePolicy` | string | Required | `resume`, `fresh`; default: `resume` | Whether conversations resume or start fresh. |
| `groundingStrategy` | string | Required | `pack`, `rag`, `both`; default: `pack` | Grounding source strategy. |
```

- [ ] **Step 4: Remove the false validation promises**

In the `## Validation rules` section of `docs/config-schema.md`, delete these two bullets:

```markdown
- `agent.json.groundingStrategy` must be one of `pack`, `rag`, or `both`.
- `agent.json.conversationResumePolicy` must be one of `resume` or `fresh`.
```

and replace this bullet:

```markdown
- Unknown values for `voice.type`, `turntaking.activeMode`, `agent.groundingStrategy`, or `agent.conversationResumePolicy` fail fast at startup.
```

with:

```markdown
- Unknown values for `voice.type` or `turntaking.activeMode` fail fast at startup.
- Keys not listed in this document are **ignored**, not rejected. Adding an undocumented key to a config file changes nothing and produces no warning.
```

- [ ] **Step 5: Run the tests**

Run: `dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true --filter "FullyQualifiedName~DocumentationTests"`

Expected: `Agent_config_ships_no_keys_the_code_never_reads` **PASS**, `Config_schema_documents_no_unimplemented_agent_keys` **PASS**. Task 2's two tests and the voice-type test still fail.

- [ ] **Step 6: Confirm config still loads**

Run: `dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true --filter "FullyQualifiedName~ServerSessionConfigTests"`

Expected: **all PASS.** These tests parse the real `config/` directory; a malformed `agent.json` fails here.

- [ ] **Step 7: Commit**

```bash
git add config/agent.json docs/config-schema.md
git commit -m "docs: remove agent.json keys that no code reads"
```

### Code-review corrections applied (2026-08-05)

**Finding 1 (HIGH) — stale prose references to deleted keys:**
- Removed "resume policy, grounding strategy" from `docs/runbook.md:76`.
- Removed "grounding strategy, resume policy" from `README.md:180`.
- Updated the plan's final-sweep grep pattern to also cover space-separated prose forms (`grounding strategy\|resume policy`) so this class of miss cannot recur.

**Finding 2 (MEDIUM) — "keys are ignored" claim was false for two files:**
- Rewrote `docs/config-schema.md` line 104 to document the asymmetric strictness: `agent.json` and `session.json` silently ignore unknown properties; `turntaking.json` validates every key under `modes` (empirically confirmed error: `turntaking.json: modes.experimental.turnDetection: is required when manualTurn is false`); `avatar.json.customized` is rejected outright. Scoped the note to exclude `appsettings.json`/environment variables.
- Corrected `docs/config-schema.md` line 107: `avatar.json.customized` is **rejected at startup** (not ignored), even when `customized: false`.

---

## Task 8: D-04 — stop documenting `azure-custom` as supported

`WebConfig.cs:22` accepts it; `SessionOptionsBuilder.cs:55` throws on it. An operator following the schema gets a Healthy app and zero working sessions. Document reality. (Code finding **H-03** removes it from `VoiceTypes` too; this task is the documentation half and is safe on its own.)

**Files:**
- Modify: `docs/config-schema.md` (2 locations)

- [ ] **Step 1: Fix the field table row**

In `docs/config-schema.md`, replace:

```markdown
| `voice.type` | string | Required | `azure-realtime-native`, `azure-standard`, `azure-custom`, `openai`; default: `azure-realtime-native` | Voice provider/type. |
```

with:

```markdown
| `voice.type` | string | Required | `azure-realtime-native`, `azure-standard`, `openai`; default: `azure-realtime-native` | Voice provider/type. |
```

- [ ] **Step 2: Fix the validation rule and record the trap**

Replace:

```markdown
- `session.json.voice.type` must be one of `azure-realtime-native`, `azure-standard`, `azure-custom`, or `openai`.
```

with:

```markdown
- `session.json.voice.type` must be one of `azure-realtime-native`, `azure-standard`, or `openai`.
- **Known trap:** startup validation currently also accepts the value `azure-custom` even though session creation always fails on it, because no custom-voice endpoint id is configured. `/api/health` reports Healthy and every session fails at connect time. Do not use it. Tracked as finding H-03 in [`review-merged.md`](../review-merged.md).
```

- [ ] **Step 3: Run the drift test**

Run: `dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true --filter "FullyQualifiedName~Config_schema_documents_only_voice_types"`

Expected: **PASS.** The test checks that `` `azure-custom` `` does not appear on any line unless the trimmed line starts with exactly `- **Known trap:**`. The warning bullet added in Step 2 above satisfies that rule exactly. Any other occurrence — in a field table row, a bullet list, a comma-separated inline list, or plain prose — will trip the test. If you reword the warning, ensure the line still starts with `- **Known trap:**` or the test will fail.

- [ ] **Step 4: Commit**

```bash
git add docs/config-schema.md
git commit -m "docs: remove azure-custom from supported voice types"
```

---

## Task 9: D-05 — describe the CSP accurately

`README.md` calls the policy "strict". It permits `connect-src 'self' wss: https:` — outbound connections to any HTTPS or WSS host — and `style-src 'unsafe-inline'`, and omits `frame-ancestors`, `base-uri`, `form-action` and `object-src`.

**Files:**
- Modify: `README.md`

- [x] **Step 1: Replace the security-headers bullet**

In `README.md`, replace:

```markdown
- **CSP and security headers** — `X-Content-Type-Options`, `X-Frame-Options: DENY`, `Referrer-Policy`, and a strict `Content-Security-Policy` on every response.
```

with:

````markdown
- **Security headers** — `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, and a `Content-Security-Policy` on every response:

  ```
  default-src 'self'; img-src 'self' data: blob:; media-src 'self' blob:;
  connect-src 'self' wss: https:; script-src 'self'; style-src 'self' 'unsafe-inline';
  worker-src 'self' blob:
  ```

  Note the current policy is **not** maximally strict: `connect-src` permits any HTTPS/WSS host, `style-src` permits inline styles because `index.html` inlines its CSS, and `frame-ancestors`, `base-uri`, `form-action` and `object-src` are not set. Tracked as finding M-11 in [`review-merged.md`](review-merged.md).
````

- [x] **Step 2: Verify**

Run: `grep -rn "strict \`Content-Security-Policy\`\|strict CSP" README.md web/README.md docs/*.md`

Expected: **no output.**

- [x] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: describe the actual CSP instead of calling it strict"
```

---

## Task 10: D-06 — replace published credentials with user-secrets instructions

Five code-formatted occurrences of the dev password across four documents, plus the committed `Auth` block. Removing only the docs would leave a broken quickstart, so this task also wires up `dotnet user-secrets` — the minimal code change that makes the new instructions true. This closes the documentation half of **C-02**.

**Carry-forward 1 applied (from Task 2 review):** Added a `File.Exists` guard to `Development_settings_carry_no_auth_section` in `DocumentationTests.cs`. Decision: missing file should **fail** — the test guards against committed credentials, so a missing file means we cannot verify the guard holds. Vacuous pass would be unsafe.

**Carry-forward 2 applied (from Task 2 review):** Added a comment on `ForbiddenCredentialLiterals` in `DocumentationTests.cs` explaining that each entry is the rendered inline-code (backtick-wrapped) form of the secret. A fenced-block or quoted-JSON appearance needs its own entry.

**AuthTests impact:** `ConfigEndpointTests` depended on the committed "operator"/"rehearsal" credentials. Fixed by adding `Auth:Username` / `Auth:Password` via `UseSetting` in `TestAppFactory`, and updating `ConfigEndpointTests` to use `TestAppFactory.TestUsername` / `TestAppFactory.TestPassword`.

**"Single shared credential" claim verified:** Confirmed against auth code — one username/password pair from `AuthOptions`; `IsConfigured` gates all logins; no per-user accounts or roles.

**Test result:** 95 passed / 2 failed / 97 total. Failures: `Every_docs_image_is_referenced_by_maintained_markdown` (Task 17) and `Maintained_markdown_has_no_broken_relative_links` (forward refs to `docs/adr/0003-shared-cookie-authentication.md`, `CONTRIBUTING.md`, `docs/production-deployment.md` intentionally not removed).

**Files:**
- Modify: `web/src/VoiceLive.Web/VoiceLive.Web.csproj`
- Modify: `web/src/VoiceLive.Web/appsettings.Development.json`
- Modify: `README.md` (2 locations)
- Modify: `web/README.md`
- Modify: `docs/runbook.md`
- Modify: `docs/config-schema.md`
- Modify: `web/tests/VoiceLive.Web.Tests/DocumentationTests.cs` (carry-forwards 1 & 2)
- Modify: `web/tests/VoiceLive.Web.Tests/TestAppFactory.cs` (add auth credentials for tests)
- Modify: `web/tests/VoiceLive.Web.Tests/ConfigEndpointTests.cs` (use TestAppFactory credentials)

- [x] **Step 1: Enable user secrets on the web project**
- [x] **Step 2: Remove the committed credentials**
- [x] **Step 3: Verify the app now demands explicit credentials** — Build succeeded.
- [x] **Step 4: Rewrite the README quickstart sign-in step**
- [x] **Step 5: Fix the README trust-boundary bullet**
- [x] **Step 6: Fix `web/README.md`**
- [x] **Step 7: Fix `docs/runbook.md` §6**
- [x] **Step 8: Fix `docs/config-schema.md`**
- [x] **Step 9: Run the credential tests** — both new passes confirmed.
- [x] **Step 10: Run the full backend suite** — 95 passed / 2 failed as expected.
- [x] **Step 11: Commit**

---

## Task 11: D-11/D-24 — reconcile the RBAC role names

Three documents disagree. **Resolved by inspecting the role GUIDs in `infra/resources.bicep`:**

| GUID | Actual role name |
|---|---|
| `a97b65f3-24c7-4388-baec-2e87135dc908` | **Cognitive Services User** |
| `53ca6127-db72-4b80-b1b0-d745d6d5456d` | **Foundry User** (Microsoft's earlier display name for this same role was *Azure AI User*) |

So `README.md` is correct and the runbook and checklist are stale — "Azure AI User" is not an *alternative* role, it is the **former name of the same role**. Say so once, in one place, and add a drift test.

**Spec contradiction resolved (Task 11 implementation note):** Step 1's original test asserted no maintained markdown contained "Azure AI User" at all, but Step 4's replacement text included `*Azure AI User*`. The stated intent — say so *once*, as a historical note, not as an alternative role — was resolved by narrowing the guard: a line is exempt only if its trimmed text starts with `- **Former name:**` AND does not also contain `Foundry User`. This means the single explanatory sub-bullet in the runbook passes, while any "Foundry User / Azure AI User" alternative-role framing fails even if prefixed with the marker.

**Files:**
- Modify: `docs/runbook.md` (2 locations)
- Modify: `docs/rehearsal-checklist.md`
- Modify: `web/tests/VoiceLive.Web.Tests/DocumentationTests.cs`

- [ ] **Step 1: Write the failing test**

Append inside the `DocumentationTests` class:

```csharp
    [Fact]
    public void Documented_rbac_roles_match_the_bicep_role_assignments()
    {
        var root = RepoRoot;
        var bicep = File.ReadAllText(Path.Combine(root, "infra", "resources.bicep"));

        // If either GUID changes, the role names in the docs are no longer trustworthy.
        Assert.True(bicep.Contains("a97b65f3-24c7-4388-baec-2e87135dc908", StringComparison.Ordinal),
            "infra/resources.bicep no longer assigns Cognitive Services User; update the RBAC docs.");
        Assert.True(bicep.Contains("53ca6127-db72-4b80-b1b0-d745d6d5456d", StringComparison.Ordinal),
            "infra/resources.bicep no longer assigns Foundry User; update the RBAC docs.");

        // Verify scopes: Cognitive Services User must be on the account (raCog scope: ai),
        // Foundry User must be on the project (raProj scope: project). This prevents README/bicep
        // drift — RBAC is never inherited child→parent, so wrong scoping causes 403 at connect.
        Assert.True(
            System.Text.RegularExpressions.Regex.IsMatch(bicep,
                @"resource raCog\b[^{]*\{[^}]*scope:\s*ai\b",
                System.Text.RegularExpressions.RegexOptions.Singleline),
            "infra/resources.bicep: raCog (Cognitive Services User) must be scoped to 'ai' (the account), not the project.");
        Assert.True(
            System.Text.RegularExpressions.Regex.IsMatch(bicep,
                @"resource raProj\b[^{]*\{[^}]*scope:\s*project\b",
                System.Text.RegularExpressions.RegexOptions.Singleline),
            "infra/resources.bicep: raProj (Foundry User) must be scoped to 'project', not the account.");

        // 'Azure AI User' is the retired display name of the Foundry User role
        // (53ca6127-db72-4b80-b1b0-d745d6d5456d), not a second role. Listing it as an
        // alternative creates three-document disagreement and confuses new operators.
        // Allowed on a line whose trimmed text starts with exactly "- **Former name:**" —
        // the single explanatory note in docs/runbook.md that tells readers the Azure portal
        // may show the old name. Even on that line, alternative-role framing is banned:
        // do not use / or "or" as alternatives, and do not write "in addition to",
        // "as applicable", "second role", "additional role", or "also grant".
        // If you are writing docs/production-deployment.md: list the two roles as
        // "`Cognitive Services User` and `Foundry User`", not "`Foundry User` / `Azure AI User`".
        var stale = MaintainedMarkdown()
            .SelectMany(rel =>
            {
                var lines = File.ReadAllLines(Path.Combine(root, rel));
                return lines
                    .Select((line, i) => (rel, line, i))
                    .Where(x =>
                    {
                        if (!x.line.Contains("Azure AI User", StringComparison.Ordinal)) return false;
                        if (!x.line.TrimStart().StartsWith("- **Former name:**", StringComparison.Ordinal)) return true;
                        // Even on the exempt line, alternative-role framing is still banned.
                        return x.line.Contains(" / `", StringComparison.Ordinal) ||
                               x.line.Contains("` / ", StringComparison.Ordinal) ||
                               x.line.Contains(" or `", StringComparison.Ordinal) ||
                               x.line.Contains("` or ", StringComparison.Ordinal) ||
                               x.line.Contains("in addition to", StringComparison.OrdinalIgnoreCase) ||
                               x.line.Contains("as applicable", StringComparison.OrdinalIgnoreCase) ||
                               x.line.Contains("second role", StringComparison.OrdinalIgnoreCase) ||
                               x.line.Contains("additional role", StringComparison.OrdinalIgnoreCase) ||
                               x.line.Contains("also grant", StringComparison.OrdinalIgnoreCase);
                    });
            })
            .Select(x => $"{x.rel} line {x.i + 1}: {x.line.Trim()}")
            .ToList();

        Assert.True(stale.Count == 0,
            "'Azure AI User' is the retired display name of Foundry User " +
            "(53ca6127-db72-4b80-b1b0-d745d6d5456d), not a separate role. " +
            "Use '`Cognitive Services User` and `Foundry User`' instead. " +
            "One mention is allowed on a line whose trimmed text starts with exactly " +
            "'- **Former name:**', but even there alternative-role framing is banned: " +
            "do not use / or 'or' as alternatives between role names, and do not include " +
            "'in addition to', 'as applicable', 'second role', 'additional role', or 'also grant'.\n  " +
            string.Join("\n  ", stale));
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true --filter "FullyQualifiedName~Documented_rbac_roles_match"`

Expected: **FAIL** listing `docs/runbook.md` and `docs/rehearsal-checklist.md`.

- [ ] **Step 3: Fix the runbook prerequisites line**

In `docs/runbook.md`, replace:

```markdown
- RBAC role assignments granting the app managed identity `Cognitive Services User` plus `Azure AI User` / `Foundry User` on the account/project.
```

with:

```markdown
- RBAC role assignments granting the app managed identity `Cognitive Services User` and `Foundry User` on the account/project.
```

- [ ] **Step 4: Fix the runbook role list**

Replace:

```markdown
- `Cognitive Services User`
- `Foundry User` / `Azure AI User` as applicable to the Foundry account/project
```

with:

```markdown
- `Cognitive Services User` (`a97b65f3-24c7-4388-baec-2e87135dc908`)
- `Foundry User` (`53ca6127-db72-4b80-b1b0-d745d6d5456d`) on the Foundry account/project.
  - **Former name:** The Azure portal may still show this role under its former display name, *Azure AI User* — it is the same role definition, so assign by GUID if the names are confusing.
```

- [ ] **Step 5: Fix the rehearsal checklist**

In `docs/rehearsal-checklist.md`, replace:

```markdown
- [ ] Confirm app/managed-identity RBAC: `Cognitive Services User` + `Foundry User` / `Azure AI User` on the account/project scope.
```

with:

```markdown
- [ ] Confirm app/managed-identity RBAC: `Cognitive Services User` + `Foundry User` on the account/project scope.
```

- [ ] **Step 6: Run the test to verify it passes**

Run: `dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true --filter "FullyQualifiedName~Documented_rbac_roles_match"`

Expected: **PASS**.

- [ ] **Step 7: Commit**

```bash
git add docs/runbook.md docs/rehearsal-checklist.md web/tests/VoiceLive.Web.Tests/DocumentationTests.cs
git commit -m "docs: reconcile RBAC role names against bicep role GUIDs"
```

---

## Task 12: D-13 — put the *why* and the non-goals in the README

The repository's best rationale is buried in a file the README itself labels "original design specification". Lift it to the top of the README, and add the non-goals that no document currently states.

**Files:**
- Modify: `README.md`
- Modify: `docs/initial-spec.md`

- [ ] **Step 1: Insert a "Why this exists" section**

In `README.md`, immediately after the opening paragraph and before the "How it works" section, insert:

```markdown
## Why this exists

This avatar converses **on stage with a C-level leader**, explaining company direction to a live audience, in a room that may be noisy. That single scenario, not a general chatbot use case, drives every design decision here.

**Reliability and rehearsability beat features.** Anything that can fail mid-show needs a defined behaviour and an operator control. The consequences run through the whole codebase:

| Decision | Because |
|---|---|
| Hold-to-talk turn gating is the default | An open microphone in a noisy room triggers on audience noise. The operator decides when the avatar listens. |
| Safe questions are one click away | If live Q&A stalls, the operator injects a known-good prompt rather than improvising. |
| Voice-only fallback when avatar capacity is unavailable | A missing video stream degrades the show; a failed session ends it. |
| Deep noise suppression and server-side VAD | Stage audio is hostile. |
| Failures are explicit, never masked | A silent retry on stage is indistinguishable from a hang. Every failure surfaces in the operator view with an action. |
| A dedicated operator view, separate from the display view | The audience must never see diagnostics. |
| A written rehearsal checklist | The show is rehearsed, so the software must be too. |

## Non-goals

Stating these plainly, because the architecture only makes sense against them:

- **Not multi-tenant and not multi-user.** Authentication is one shared username and password. Everyone who signs in is the same principal, and there is no per-operator identity, audit trail, or authorization model.
- **Not internet-facing.** See [Production readiness](#production-readiness) below before exposing this to an untrusted network.
- **Not a persistent assistant.** There is no conversation storage, no cross-session memory, and no user profile.
- **Not horizontally scalable as configured.** The concurrency cap is a per-instance in-memory gate; scaling out multiplies it rather than sharing it.
- **One session per browser tab.** Opening the operator and display views simultaneously consumes two of the two available session slots.
```

- [ ] **Step 2: Mark the spec as historical**

At the very top of `docs/initial-spec.md`, immediately under the H1 heading, insert:

```markdown
> **Status: historical.** This is the original design specification, retained for context. It records intent at the time of writing and is **not** maintained against the current implementation. For behaviour that is warranted accurate, see [`docs/README.md`](README.md). The use case and design rationale in §1 have been promoted to the [project README](../README.md#why-this-exists).
```

- [ ] **Step 3: Verify the anchor targets exist**

Run: `grep -n "^## Why this exists\|^## Non-goals\|^## Production readiness" README.md`

Expected: `Why this exists` and `Non-goals` are present. `Production readiness` is added in Task 13 — the `#production-readiness` link in Step 1 is a deliberate forward reference and will resolve then. (The link test only checks file targets, not anchors, so it stays green.)

- [ ] **Step 4: Commit**

```bash
git add README.md docs/initial-spec.md
git commit -m "docs: promote the use case, design rationale and non-goals into the README"
```

---

## Task 13: D-17 — add the production-readiness gate

The README currently walks a reader to a public HTTPS endpoint guarded by one shared password, while both reviews conclude the app is not ready for untrusted users. This is the highest-severity documentation finding: the docs invite the deployment the reviews warn against.

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Insert the section immediately before "Deploy to Azure"**

In `README.md`, insert directly above the `## Deploy to Azure` heading:

```markdown
## Production readiness

**Read this before exposing the app to any network you do not control.**

As shipped, this application is built for a **rehearsed, operator-attended, single-event deployment on a trusted network**. Two independent security reviews of commit `d5110dc` ([`review-merged.md`](review-merged.md)) concluded it is not ready for untrusted or internet-facing users. Nothing about the deployment path below enforces that boundary — `azd up` produces a public HTTPS endpoint protected by a single shared password.

Close these before an exposed deployment. IDs link to the finding detail.

| # | Finding | Required action |
|---|---|---|
| 1 | **C-02** | Remove committed credentials, move to `dotnet user-secrets` locally, and rotate the resource if the committed values were ever real. |
| 2 | **C-01** | Configure `ForwardedHeadersOptions` with known proxies and partition the rate limiter on the validated client IP; today the per-IP limiter is bypassable by a forged header. |
| 3 | **H-01** | Constrain the `say` control frame to a server-side allow-list, with a length cap and per-connection rate limit. Any authenticated client can currently make the avatar speak arbitrary text on stage. |
| 4 | **M-01** | Add absolute and idle session timeouts. There is no timeout today, and the service bills per session-minute. |
| 5 | **M-02** | Move `Auth__Password` out of plaintext App Service settings into a Key Vault reference. |
| 6 | **H-02** | Add antiforgery protection to `POST /login`. |
| 7 | **H-05** | Make blocked autoplay recoverable instead of terminating the session. |

**Also required, and not covered by the code findings above:** decide the identity model (a single shared credential is the whole authentication story today), plan avatar-rendering quota ahead of the event, set up alerting on `/api/health`, and agree a rollback procedure. See [`docs/production-deployment.md`](docs/production-deployment.md).
```

- [ ] **Step 2: Verify placement and the back-link from Task 12**

Run: `grep -n "^## Production readiness\|^## Deploy to Azure\|#production-readiness" README.md`

Expected: `## Production readiness` appears immediately before `## Deploy to Azure`, and the `#production-readiness` reference from the Non-goals section now has a matching heading.

- [ ] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: add production-readiness gate before the deploy instructions"
```

---

## Task 14: D-14, D-15, D-16, D-21 — repair getting started

Four defects: no document names a test command, `web/README.md` ships a personal absolute path, the quickstart has no way to verify prerequisites, and the runbook embeds a point-in-time test result as if it were procedure.

**Files:**
- Modify: `web/README.md`
- Modify: `README.md`
- Modify: `docs/runbook.md`

- [ ] **Step 1: Fix the hardcoded absolute path (D-15)**

In `web/README.md`, replace the absolute path with a repo-relative one:

```bash
ConfigDir=$(pwd)/config ASPNETCORE_URLS=http://127.0.0.1:5210 dotnet run --project src/VoiceLive.Web
```

Add immediately below it:

```markdown
Run this from the repository root. `ConfigDir` must be an absolute path, which is why `$(pwd)` is used rather than `./config`.
```

- [ ] **Step 2: Verify the path is gone**

Run: `grep -rn "/home/" README.md web/README.md docs/*.md`

Expected: **no output.**

- [ ] **Step 3: Add prerequisite verification to the README quickstart (D-16)**

In `README.md`, at the start of the quickstart section, insert:

````markdown
Verify the toolchain and your Azure access before the first run — a missing role assignment is the most common first-run failure:

```bash
dotnet --version   # 10.0 or later
node --version     # 20 or later
python3 --version  # required by the Playwright suite's static file server
az account show --query '{sub:name, user:user.name}' -o table
az role assignment list --assignee "$(az ad signed-in-user show --query id -o tsv)" \
  --query "[].roleDefinitionName" -o tsv
```

The last command must list **Cognitive Services User** and **Foundry User**. If it does not, session creation will fail at connect time with a `403` even though `/api/health` reports Healthy.
````

- [ ] **Step 4: Explain `session.sample.json` (D-16)**

In the `README.md` config-directory listing, replace the `session.sample.json` description with:

```markdown
- `session.sample.json` — a reference copy of `session.json`, excluded from publish. Copy it over `session.json` to return to known-good settings after experimenting, and diff against it when a config change causes a startup validation failure.
```

- [ ] **Step 5: Add a Development section pointing at the tests (D-14)**

In `README.md`, insert a new section directly before `## Production readiness`:

````markdown
## Development

Full setup, prerequisites and conventions are in [CONTRIBUTING.md](CONTRIBUTING.md). The commands you need most:

```bash
# Backend tests — skip the frontend build for speed, as CI does
dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true

# Frontend type check
npm --prefix web/frontend run typecheck

# Playwright end-to-end tests (requires Python 3 on PATH for the static server)
npm --prefix web/frontend test
```
````

- [ ] **Step 6: Remove point-in-time test evidence from the runbook (D-21)**

In `docs/runbook.md` §7, delete this sentence entirely:

```markdown
A headless browser E2E reached WebRTC `connected` state with video and audio tracks arriving, and the safe-question path produced streaming transcripts plus a completed response.
```

Replace it with procedure rather than a past result:

```markdown
To confirm the avatar path end to end, run the Playwright suite (`npm --prefix web/frontend test`) and watch the WebRTC status indicator reach **connected** in the operator view, with video and audio tracks arriving and a safe question producing streaming transcripts followed by a completed response.
```

- [ ] **Step 7: Verify the documented commands actually work**

Run each and confirm it is a real target:

```bash
dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true
npm --prefix web/frontend run typecheck
```

Expected: backend tests pass except the still-outstanding `Every_docs_image_is_referenced_by_maintained_markdown` (Task 17) and the link guard (see the forward-reference table). `npm run typecheck` runs `tsc --noEmit` and exits 0.

Note `npm --prefix web/frontend test` runs `typecheck && test:e2e`, and `test:e2e` builds the bundle then runs Playwright — so it needs Python 3 on `PATH`. That is why Step 3 adds `python3 --version` to the prerequisite checks.

- [ ] **Step 8: Commit**

```bash
git add README.md web/README.md docs/runbook.md
git commit -m "docs: fix getting-started gaps — test commands, prereq checks, portable paths"
```

---

## Task 15: D-18, D-19, D-20 — write the production deployment guide

The largest gap in the documentation set. `docs/runbook.md` is a *rehearsal* runbook; nothing covers identity, secrets, capacity, cost, day-2 operations, rollback or DR.

**Files:**
- Create: `docs/production-deployment.md`
- Modify: `docs/runbook.md`

- [ ] **Step 1: Create the guide**

Create `docs/production-deployment.md`:

````markdown
# Production deployment

**Scope.** How to run this application for a real event with real stakes. [`runbook.md`](runbook.md) covers provisioning and rehearsal; [`rehearsal-checklist.md`](rehearsal-checklist.md) covers the hours before showtime. This document covers everything between: identity, secrets, capacity, cost, observability, environments, rollback and disaster recovery.

**Read [Production readiness](../README.md#production-readiness) first.** The gate list there is a hard prerequisite for this document.

## 1. Identity model

**What ships:** a single shared username and password, validated by custom middleware, issuing an 8-hour sliding cookie. Everyone who signs in is the same principal.

**What that means:** there is no per-operator identity, no audit trail attributable to a person, no way to revoke one operator's access without changing the password for everyone, and no authorization model — every authenticated user can reach every endpoint, including `say`, which makes the avatar speak arbitrary text on stage.

**Acceptable when:** the app is on a trusted network, the audience of the credential is a named handful of people, and the event is attended.

**Not acceptable when:** the app is internet-reachable, the credential is shared beyond the event team, or the deployment outlives the event.

**The standard remedy** is App Service Easy Auth with Microsoft Entra ID, which removes credential custody from the application entirely and gives per-operator identity and revocation. It is not implemented here. If you need it, treat it as a prerequisite project, not a deployment-time toggle.

## 2. Secrets

**Never** set the operator password with `azd env set AUTH_PASSWORD <password>` for a production deployment. That lands the sole credential in plaintext App Service configuration, readable by anyone with Reader on the resource.

Use a Key Vault reference instead:

```bash
az keyvault secret set --vault-name <vault> --name auth-password --value "<password>"

az webapp config appsettings set --name <app> --resource-group <rg> --settings \
  "Auth__Password=@Microsoft.KeyVault(VaultName=<vault>;SecretName=auth-password)"
```

The web app's system-assigned managed identity needs **Key Vault Secrets User** on the vault. Verify resolution before the event — a failed reference surfaces as the literal `@Microsoft.KeyVault(...)` string becoming the password, so **sign in successfully after every secret change**.

**Rotation.** Rotate after every event and whenever anyone with the credential leaves the team. Rotation is a secret update plus an app restart; sessions signed in with the old cookie survive up to 8 hours because the cookie is self-contained and is not revoked by a password change. If you need immediate revocation, restart the app *and* change `Auth:Username`, which invalidates the cookie's identity.

**Never** commit credentials. `appsettings.Development.json` no longer carries an `Auth` section, and a test enforces that.

## 3. Capacity and quota

Three independent limits, in the order you will hit them:

| Limit | Value | Behaviour when exceeded | Where to change |
|---|---|---|---|
| Concurrent app sessions | `MaxConcurrentSessions`, default **2** | New connections are rejected at the gate | `config/session.json` |
| Avatar rendering quota | Per Azure AI Foundry resource | `avatar_service_resource_exhausted`; the app falls back to voice-only | Azure quota request |
| App Service instance | B1, single instance | CPU saturation and dropped audio | App Service plan |

**The concurrency gate is per-instance and in-memory.** Scaling out to N instances does not share the cap — it multiplies it to N × `MaxConcurrentSessions`, silently. **Do not scale out to increase capacity.** Scale up instead, and raise `MaxConcurrentSessions` deliberately, having tested the instance can carry the load.

**Each browser tab is a session.** An operator view plus a display view is two sessions — the entire default budget. Plan the slot count against the number of tabs you will actually open, plus one spare for a mid-show reconnect.

**Request avatar quota before the event, not on the day.** Quota approval is not instant, and the failure mode is silent degradation to voice-only, which is exactly the outcome the avatar exists to avoid.

## 4. Cost

Voice Live bills **per session-minute**, and **there is no session timeout in this application** (finding M-01). A forgotten browser tab holds a session open and bills until the tab closes, the app restarts, or the socket drops.

**Guardrails, in order of effectiveness:**

1. Close every tab at the end of the event. This is the only control that exists today; put it on the teardown checklist.
2. Stop the App Service between rehearsal and event day. Sessions cannot outlive the process.
3. Set a budget alert on the resource group so an overrun is noticed in hours, not on the invoice.
4. Implement M-01 (absolute + idle timeouts) if this deployment will run unattended at any point.

Cost drivers, largest first: avatar rendering minutes, realtime model audio minutes, then App Service compute, then Application Insights ingestion. The fixed infrastructure cost is trivial next to a session left open over a weekend.

## 5. Observability

Application Insights and Log Analytics are provisioned, and the app emits OpenTelemetry metrics — but **no alert exists until you create one.** Provisioned telemetry that nobody watches is not observability.

**Minimum alert set before an event:**

| Alert | Signal | Why |
|---|---|---|
| Health degraded | `/api/health` availability test, non-200 for 2 consecutive minutes | Catches invalid config and lost RBAC before showtime |
| Session start failures | Exception rate on the session-start path > 0 over 5 minutes | The `403`/`429`/quota failures that end a show |
| Capacity rejections | Gate-rejection count > 0 | Someone opened one tab too many |
| Instance health | CPU > 80% for 5 minutes | B1 saturation drops audio |

Useful Log Analytics queries:

```kusto
// Failed session starts in the last hour, by reason
AppExceptions
| where TimeGenerated > ago(1h)
| summarize count() by ProblemId, bin(TimeGenerated, 5m)
| order by TimeGenerated desc

// Health endpoint status over the last 6 hours
AppRequests
| where TimeGenerated > ago(6h) and Url endswith "/api/health"
| summarize count() by ResultCode, bin(TimeGenerated, 15m)

// Avatar quota exhaustion — the silent degrade to voice-only
AppTraces
| where TimeGenerated > ago(24h) and Message has "avatar_service_resource_exhausted"
| project TimeGenerated, Message
```

**Suggested SLO for an event window:** 100% availability of `/api/health` and zero failed session starts during the show, measured over the rehearsal-to-teardown window rather than a rolling month. An event either works or it does not; a monthly error budget is the wrong instrument.

**Diagnostic settings are not configured by default** (finding L-04). Route App Service and Foundry resource logs to the Log Analytics workspace before the event, or post-incident analysis will have nothing to read.

## 6. Environments and deployment

There is **no CD pipeline**. CI builds and tests but never deploys and never runs `dotnet publish`, so the artifact-producing path is not exercised by automation (finding L-17). Deployment is a manual `azd up`.

**Minimum viable environment model:**

| Environment | Purpose | Provisioning |
|---|---|---|
| `dev` | Local, config from `config/`, credentials from user-secrets | `dotnet run` |
| `rehearsal` | Full Azure deployment, same region and SKU as production, used for the rehearsal checklist | `azd up` with its own `azd` environment |
| `event` | The deployment the show runs on | `azd up` with its own `azd` environment |

Use **separate `azd` environments**, not a shared one — a shared environment means the rehearsal deploy and the event deploy are the same resources, so any rehearsal change is a production change.

**Deploy at least 24 hours before the event**, then freeze. Run the full [rehearsal checklist](rehearsal-checklist.md) against the frozen deployment.

## 7. Rollback

The most important production procedure, and the fastest.

```bash
# List recent deployments, newest first
az webapp deployment list --name <app> --resource-group <rg> \
  --query "[].{id:id, time:received_time, active:active}" -o table

# Roll back to a previous deployment
az webapp deployment source config-zip --name <app> --resource-group <rg> --src <previous.zip>
```

**Prepare a rollback before the event:** keep the last known-good published artifact, and record its deployment id in the event runbook. Mid-show is not when you discover the artifact is gone.

**Configuration rollback is separate.** Config is read from `config/` **at startup only** and there is no hot reload (finding L-20). Changing config requires an app restart, which drops every live session. **Never edit config during a show.** Treat `config/` changes as deployments: change, restart, re-verify `/api/health`, re-run the smoke test.

## 8. Business continuity

The whole project exists to serve one high-stakes live moment, so plan for the region being degraded 30 minutes before it.

| Scenario | Prepared fallback |
|---|---|
| Foundry region degraded | Pre-provision a second `azd` environment in an alternate region **that supports native realtime voice, avatar and agent mode**. Verify it during rehearsal — an untested standby is not a standby. |
| Avatar quota exhausted | Voice-only mode already degrades automatically. Brief the speaker beforehand so a missing avatar is not a surprise on stage. |
| App Service unreachable | Have the pre-recorded segment or static slides ready. Agree the abort call and who makes it. |
| Network loss in the venue | The media plane is direct browser↔Azure WebRTC; there is no offline mode. Venue connectivity is a single point of failure — test it from the actual stage position, on the actual network, during rehearsal. |

Write the abort decision into the event runbook: **who** calls it, **when**, and **what** replaces the segment.

## 9. Networking

Default `azd up` produces a public endpoint with the App Service default hostname.

- **Custom domain and TLS** — bind a custom domain with an App Service managed certificate if the URL is visible to the audience.
- **Access restrictions** — the highest-value single hardening step. Restrict inbound access to the venue's egress IP range:

  ```bash
  az webapp config access-restriction add --name <app> --resource-group <rg> \
    --rule-name venue --action Allow --ip-address <venue-cidr> --priority 100
  ```

  This converts "one shared password on the public internet" into "one shared password on a network you control", which is the assumption the whole design rests on.
- **Private endpoints / VNet integration** are not configured (finding L-03). Consider them if the app must reach Foundry over a private path.
- **`AllowedHosts` is `*`** (finding M-07). Set it to the actual hostname.

## 10. Data handling and privacy

**Applies to every deployment. Confirm before an event with real attendees.**

- **Microphone audio** is streamed to Azure Foundry Voice Live for the duration of a turn. This application does not write audio to disk and does not persist it.
- **Transcripts** are relayed to the browser for display and are not persisted server-side. They exist in browser memory until the tab closes.
- **Conversations are not stored.** There is no history, no cross-session memory and no user profile.
- **Application Insights** captures request telemetry and exceptions. Confirm no transcript content reaches log messages before deploying anywhere with real attendee speech.
- **Azure-side retention** is governed by your Foundry resource configuration, not by this application. Review the abuse-monitoring and data-retention settings on the Foundry resource and, if required, apply for the limited-access exemption from human review.
- **Region** is pinned to `swedencentral`, keeping processing in the EU. Changing the region changes where speech is processed — a compliance decision, not just a latency one. See [ADR 0006](adr/0006-region-pinned-swedencentral.md).

**Tell the audience.** If audience speech can reach the microphone, that is a recording notice obligation in most jurisdictions.
````

- [ ] **Step 2: Link the guide from the runbook**

At the top of `docs/runbook.md`, immediately under the H1, insert:

```markdown
> **Scope:** provisioning and **rehearsal**. For identity, secrets, capacity, cost, alerting, rollback and disaster recovery, see [`production-deployment.md`](production-deployment.md).
```

- [ ] **Step 3: Verify links resolve**

Run: `dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true --filter "FullyQualifiedName~Maintained_markdown_has_no_broken_relative_links"`

Expected: still failing only on entries in the forward-reference table above that have not yet been resolved — `CONTRIBUTING.md`, `docs/adr/0003-…`, `docs/adr/0006-…`, `docs/README.md`. `docs/production-deployment.md` is now resolved. No *new* categories of broken link.

- [ ] **Step 4: Commit**

```bash
git add docs/production-deployment.md docs/runbook.md
git commit -m "docs: add production deployment guide"
```

---

## Task 16: D-11, D-24 — one authoritative wire-protocol reference

The endpoint table and frame vocabulary appear in three documents with no source of truth, and frame *shapes* are documented nowhere. Write the reference, then make the other documents link to it instead of restating it.

**Files:**
- Create: `docs/wire-protocol.md`
- Modify: `web/README.md`

- [ ] **Step 1: Create the reference**

Create `docs/wire-protocol.md`:

````markdown
# Wire protocol reference

**Authoritative reference for `/ws/session`.** If another document contradicts this one, this one is correct — and the other document is a bug. Do not restate frame vocabulary elsewhere; link here.

Verified against `web/frontend/src/main.ts`, `web/frontend/src/views.ts` and the server bridge at commit `d5110dc`.

## Endpoints

| Method | Path | Auth | Purpose |
|---|---|---|---|
| `GET` | `/` | Cookie | Application shell. `?view=operator`, `?view=display`, or the default landing view. |
| `GET` | `/login` | Anonymous | Sign-in form. |
| `POST` | `/login` | Anonymous | Credential submission; issues the auth cookie. |
| `POST` | `/logout` | Cookie | Clears the auth cookie. |
| `GET` | `/api/health` | Anonymous | Health and configuration-validity report. |
| `GET` | `/ws/session` | Cookie | WebSocket upgrade. One connection = one Voice Live session = one concurrency slot. |

## Connection lifecycle

1. Browser opens the WebSocket to `/ws/session` with the auth cookie.
2. Server validates the cookie and the `Origin` header, then acquires a slot from the concurrency gate. Rejection closes the socket.
3. Server acquires an Azure token via `DefaultAzureCredential`, builds session options and connects upstream to Voice Live. **The browser never receives an Azure token.**
4. Server sends `ready`. The client must wait for `ready` before sending anything else.
5. If the avatar is enabled, the browser sends `avatar-offer`; the server relays SDP and replies with `avatar-answer`. Media then flows **directly** between browser and Azure over WebRTC — not through the server. See [ADR 0002](adr/0002-direct-webrtc-media-plane.md).
6. Turns proceed (below). Audio uplink is binary; everything else is JSON text.
7. Either side closing releases the concurrency slot.

## Browser → server

| Frame | Payload | When |
|---|---|---|
| *(binary)* | PCM16 mono audio | Continuously while the microphone is streaming. Not JSON — raw binary frames. |
| `avatar-offer` | `{ "type": "avatar-offer", "sdp": string }` | Once, after `ready`, when the avatar is enabled. |
| `start-turn` | `{ "type": "start-turn" }` | Operator begins speaking (press of **Hold to talk**). Sets microphone streaming on. |
| `end-turn` | `{ "type": "end-turn" }` | Operator stops speaking (release). Sets microphone streaming off. |
| `barge-in` | `{ "type": "barge-in" }` | Operator interrupts avatar speech. |
| `say` | `{ "type": "say", "text": string }` | Safe-question injection. **Unconstrained today** — see finding H-01. |
| `ping` | `{ "type": "ping" }` | Keepalive. Answered with `pong`. |

## Server → browser

All are JSON text frames with a `type` discriminator.

| Frame | Payload | Meaning |
|---|---|---|
| `ready` | `{ "type": "ready", "config": ClientConfig, "iceServers": RTCIceServer[] }` | Session established. Always the first frame. |
| `user-transcript` | `{ "type": "user-transcript", "text": string, "final": boolean }` | Speech-to-text of the operator. `final: false` frames are interim and are replaced, not appended. |
| `agent-transcript` | `{ "type": "agent-transcript", "text": string, "final": boolean }` | The avatar's response text, same interim semantics. |
| `speech-started` | `{ "type": "speech-started" }` | Server-side VAD detected speech. |
| `speech-stopped` | `{ "type": "speech-stopped" }` | Server-side VAD detected end of speech. |
| `avatar-speaking` | `{ "type": "avatar-speaking" }` | Avatar audio playback began. |
| `avatar-idle` | `{ "type": "avatar-idle" }` | Avatar finished speaking. |
| `avatar-answer` | `{ "type": "avatar-answer", "sdp": string }` | WebRTC answer; the browser applies it as the remote description. |
| `response-done` | `{ "type": "response-done" }` | The turn's response is complete. |
| `tool` | `{ "type": "tool", "phase": string, "name"?: string, "callId"?: string }` | Tool invocation progress. Hosted tools may emit no client event at all. |
| `avatar-error` | `{ "type": "avatar-error", "code"?: string, "message": string }` | **Non-fatal.** Avatar failed; voice continues. `avatar_service_resource_exhausted` here means quota exhaustion → voice-only fallback. |
| `error` | `{ "type": "error", "message": string }` | **Fatal.** The session is over. The client shows an error banner and reveals **Reconnect**. |
| `pong` | `{ "type": "pong" }` | Reply to `ping`. |

### `ClientConfig`

Sent inside `ready`. Mirrors the server record of the same name.

| Field | Type | Notes |
|---|---|---|
| `mode` | string | Configured mode. |
| `activeMode` | string | Turn-taking mode actually in force: `gated`, `open`, or `hybrid`. |
| `agentName` | string or null | Populated in agent mode only. |
| `safeQuestions` | string[] | Rendered as one-click buttons in the operator view. |
| `avatarCharacter` | string | Avatar character id. |
| `avatarStyle` | string | Avatar style id. |

## Validation

**Neither side validates frame shape today.** Both ends switch on `type` and read fields optimistically, so a malformed frame produces an undefined-property error rather than a clean protocol failure. Tracked as finding M-06. This table is the contract that fix should enforce.
````

- [ ] **Step 2: Replace the duplicated tables in `web/README.md`**

Delete the endpoint table and the frame-vocabulary tables from `web/README.md` and put in their place:

```markdown
The endpoint list and the full `/ws/session` frame vocabulary — including payload shapes and which errors are fatal — are documented once, in [`docs/wire-protocol.md`](../docs/wire-protocol.md).
```

- [ ] **Step 3: Point the README at the reference**

In `README.md`, directly beneath the existing frame tables, add:

```markdown
Payload shapes, the `ClientConfig` contents of `ready`, and which errors are fatal are documented in [`docs/wire-protocol.md`](docs/wire-protocol.md), which is authoritative if this summary and that reference ever disagree.
```

- [ ] **Step 4: Verify the frame list matches the code**

Run: `grep -rno "case \"[a-z-]*\"" web/frontend/src/main.ts | sort -u`

Expected: every server→browser `type` handled in `main.ts` appears in the table above, and the table lists nothing the code does not handle. If they differ, **the code wins** — correct the document.

- [ ] **Step 5: Commit**

```bash
git add docs/wire-protocol.md web/README.md README.md
git commit -m "docs: add authoritative wire-protocol reference and de-duplicate frame tables"
```

---

## Task 17: D-07, D-08, D-09 — session flow, state model and view journeys

Three orphaned diagrams already exist in `docs/images/` and, by their filenames, are exactly the three flows the documentation lacks. Give them a home, and add the turn lifecycle, the six status channels and the per-view journeys.

**Files:**
- Create: `docs/session-flow.md`
- Modify: `README.md`

- [ ] **Step 1: Confirm the images are still there**

Run: `ls docs/images/`

Expected: `voice_live_decision_points.png`, `voice_live_prewarm_connection_flow.png`, `voice_live_single_turn_flow.png`.

- [ ] **Step 2: Create the document**

Create `docs/session-flow.md`:

````markdown
# Session flow and state

How a session starts, how a turn runs, what the six status indicators mean, and what each view can do. For frame payloads see [`wire-protocol.md`](wire-protocol.md).

## Connection flow

![Voice Live connection and pre-warm flow](images/voice_live_prewarm_connection_flow.png)

The browser holds no Azure credential at any point. The server acquires the token, opens the upstream session, and only then tells the browser it is `ready`. Avatar media is negotiated afterwards and flows directly browser↔Azure.

## A single turn

![Voice Live single turn flow](images/voice_live_single_turn_flow.png)

In the default **gated** mode a turn is explicitly bracketed by the operator:

1. Operator presses **Hold to talk** → client sends `start-turn` and begins streaming microphone audio.
2. Binary PCM16 frames flow while the button is held. Server-side VAD emits `speech-started` / `speech-stopped`.
3. Interim `user-transcript` frames arrive with `final: false`, replaced as recognition improves, then once with `final: true`.
4. Operator releases → client sends `end-turn` and **stops** streaming audio.
5. The model responds: `agent-transcript` frames stream in, `avatar-speaking` fires when audio playback begins, avatar video and audio arrive over the WebRTC media plane.
6. `avatar-idle` then `response-done` close the turn.

**Safe questions** skip steps 1–4: clicking one sends a single `say` frame and the flow resumes at step 5.

**Barge-in** sends `barge-in` during step 5 to interrupt.

### Rules and edge cases

- **Wait for `ready`.** Frames sent before it are not honoured.
- **`end-turn` without `start-turn`** is not rejected — it simply stops microphone streaming that was not running. It is a no-op, not an error.
- **Mute** toggles microphone streaming independently of the turn state. Muting mid-turn stops audio without ending the turn, so the model sees a truncated utterance.
- **Barge-in outside avatar speech** is harmless but pointless — there is nothing to interrupt.
- **Turn-taking modes:** `gated` requires explicit `start-turn`/`end-turn` (default, and the right choice on a noisy stage). `open` streams continuously and lets server VAD segment turns. `hybrid` combines both. The mode in force is reported as `activeMode` in the `ready` frame.

## Decision points

![Voice Live decision points](images/voice_live_decision_points.png)

The branch points that determine what an operator sees: model mode vs. agent mode, avatar enabled vs. voice-only, and the capacity/quota fallbacks.

## Status indicators

The UI exposes six independent status channels. All six must be in their healthy state for a working avatar session.

| Channel | Healthy value | Meaning when not healthy |
|---|---|---|
| `connection` | connected | WebSocket to the app. Disconnected → the **Reconnect** button appears. Nothing else works. |
| `webrtc` | connected | Peer connection to Azure for avatar media. Failed while `connection` is healthy → voice may still work; video will not. |
| `microphone` | ready | Browser microphone permission and capture. Denied → no input; safe questions still work. |
| `turn` | idle / active | Whether a turn is currently open. Stuck on active → a `start-turn` was never closed; release and re-press. |
| `speech` | idle / detected | Server-side VAD. Never leaving idle while speaking → the microphone is muted or capturing silence. |
| `avatar` | idle / speaking | Avatar playback. Stuck idle after `response-done` → check the `webrtc` channel. |

**Diagnostic shortcut:** `connection` healthy but `webrtc` failed is the voice-only fallback, and is survivable mid-show. `connection` failed is fatal and needs a Reconnect click.

## The three views

All three are the same app shell, selected by query string, and **each open tab is its own session consuming one concurrency slot**. With the default `MaxConcurrentSessions = 2`, an operator view plus a display view uses the entire budget.

| View | URL | Microphone | Controls | Intended screen |
|---|---|---|---|---|
| Landing | `/` | Yes | Minimal; the ⚙ gear is the only route to the operator view | Setup and testing |
| Operator | `/?view=operator` | Yes | Hold to talk, mute, safe questions, barge-in, all six status indicators, Reconnect | The operator's laptop, never visible to the audience |
| Display | `/?view=display` | **No** | Avatar video only; Reconnect appears on disconnect | The stage screen |

**Two consequences worth planning for:**

- The display view has **no microphone and no interaction affordance**, yet a browser will still block autoplay until the page receives a user gesture. **Click into the display screen once before the audience arrives** — see [`runbook.md`](runbook.md) §7.
- Reconnection is operator-initiated. An unattended display screen that disconnects stays disconnected until someone clicks Reconnect.
````

- [ ] **Step 3: Link it from the README**

In `README.md`, directly after the "How it works" section's sequence diagram, add:

```markdown
The turn lifecycle, the six status indicators and what each view can do are documented in [`docs/session-flow.md`](docs/session-flow.md).
```

- [ ] **Step 4: Run the orphaned-image test**

Run: `dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true --filter "FullyQualifiedName~Every_docs_image_is_referenced"`

Expected: **PASS** — all three images are now referenced.

- [ ] **Step 5: Commit**

```bash
git add docs/session-flow.md README.md
git commit -m "docs: document turn lifecycle, status model and view journeys"
```

---

## Task 18: D-10 — architecture decision records

The architecture is documented as a description of what exists, never as decisions with rationale and rejected alternatives. Six decisions carry the design; each has a real answer that is currently unwritten.

**Files:**
- Create: `docs/adr/README.md` and `docs/adr/0001-…` through `docs/adr/0006-…`
- Modify: `README.md`

- [ ] **Step 1: Create the index**

Create `docs/adr/README.md`:

```markdown
# Architecture decision records

Short records of the decisions that shape this codebase: the context, what was decided, what was rejected, and what it costs. Format is a trimmed [MADR](https://adr.github.io/madr/). ADRs are **immutable** — supersede rather than edit.

| # | Decision | Status |
|---|---|---|
| [0001](0001-server-side-credential-custody.md) | The browser never holds an Azure credential | Accepted |
| [0002](0002-direct-webrtc-media-plane.md) | Avatar media bypasses the server | Accepted |
| [0003](0003-shared-cookie-authentication.md) | One shared credential, app-level cookie auth | Accepted, with known limits |
| [0004](0004-startup-only-config-validation.md) | Config validated at startup, no hot reload | Accepted |
| [0005](0005-per-instance-session-cap.md) | Concurrency capped per instance, in memory | Accepted, with a scale-out trap |
| [0006](0006-region-pinned-swedencentral.md) | Region pinned to `swedencentral` | Accepted |
```

- [ ] **Step 2: Create ADR 0001**

Create `docs/adr/0001-server-side-credential-custody.md`:

```markdown
# 0001 — The browser never holds an Azure credential

**Status:** Accepted

## Context

The browser needs a live Voice Live session. The simplest implementation mints a token in the browser and connects directly to Azure.

## Decision

The server holds all Azure credentials. It acquires a token via `DefaultAzureCredential` — a managed identity in Azure, developer credentials locally — opens the upstream Voice Live session itself, and relays control and audio frames to the browser over `/ws/session`. No token, key or connection string is ever sent to the browser.

## Alternatives rejected

- **Browser-minted ephemeral tokens.** Still puts an Azure-scoped credential in a context the operator's browser extensions, the venue network and anyone with the laptop can reach. The blast radius of a leak is the Foundry resource, not this app.
- **API keys in config.** Same exposure, without expiry.

## Consequences

- The Foundry resource is never directly reachable by a client. Compromising the browser yields an app session, not Azure access.
- The server is on the audio path for the uplink, so it must be sized for concurrent audio relay.
- Local development needs a signed-in developer identity with the right roles — the most common first-run failure. See [`../production-deployment.md`](../production-deployment.md) §1.
```

- [ ] **Step 3: Create ADR 0002**

Create `docs/adr/0002-direct-webrtc-media-plane.md`:

```markdown
# 0002 — Avatar media bypasses the server

**Status:** Accepted

## Context

The avatar produces a video and audio stream. Relaying it through the app server, as the control plane is relayed, would be architecturally uniform.

## Decision

Avatar media uses WebRTC **directly between the browser and Azure**. The server relays only the SDP offer/answer and ICE configuration; once negotiated, media never touches the app.

## Alternatives rejected

- **Server-relayed media.** Adds a hop of latency to a live stage performance and makes the B1 App Service instance a video relay, which it cannot do at acceptable quality.

## Consequences

- Lowest achievable latency, and video quality is independent of app instance size — important, because the whole point is a believable on-stage presence.
- **The venue's network must reach Azure directly over WebRTC.** Restrictive venue firewalls break the avatar while leaving the control plane working, which presents as a working session with no video. Test from the actual stage position on the actual network.
- The server cannot observe, record or moderate avatar output. What Azure renders is what the audience sees.
- A separate failure domain: `avatar-error` is non-fatal and degrades to voice-only, while a control-plane `error` ends the session.
```

- [ ] **Step 4: Create ADR 0003**

Create `docs/adr/0003-shared-cookie-authentication.md`:

```markdown
# 0003 — One shared credential, app-level cookie authentication

**Status:** Accepted, with known limits

## Context

The app needs to keep the public internet away from a per-minute-billed Azure session. Its users are a handful of named people running one rehearsed event.

## Decision

A single shared username and password, validated by app middleware, issuing an 8-hour sliding cookie. No user store, no identity provider.

## Alternatives rejected

- **Microsoft Entra ID via App Service Easy Auth.** The correct long-term answer — per-operator identity, revocation, no credential custody in the app. Rejected for the initial build because it requires tenant configuration outside the repository and adds a sign-in dependency on event day, when the failure mode of an identity outage is a dead show.
- **No authentication at all.** Unacceptable: an unauthenticated public endpoint on a per-minute-billed service is a cost and abuse incident waiting to happen.

## Consequences

- **This is the entire authorization model.** Every authenticated user reaches every endpoint, including `say`, which puts arbitrary text in the avatar's mouth on stage (finding H-01).
- No audit trail attributable to a person. "Who made it say that" has no answer.
- Revoking one person means changing the password for everyone, and the 8-hour cookie keeps working until it expires.
- Consequently the app is **not internet-facing**. Combine with App Service access restrictions so the shared credential only defends a network you already control.
- Superseding this ADR with Entra ID is the single highest-value security change available.
```

- [ ] **Step 5: Create ADR 0004**

Create `docs/adr/0004-startup-only-config-validation.md`:

```markdown
# 0004 — Config validated at startup, no hot reload

**Status:** Accepted

## Context

Behaviour comes from JSON files in `config/`. They could be watched and reloaded at runtime.

## Decision

Config is read and validated **once, at startup**. Invalid config does not crash the process — the app starts and reports the problem through `/api/health`. There is no file watcher and no reload endpoint.

## Alternatives rejected

- **Hot reload.** Mid-show config changes are a footgun: a typo silently changes avatar behaviour in front of an audience, with no review and no rollback.
- **Fail-fast exit on invalid config.** Rejected because a crash-looping app on event day gives an operator nothing to diagnose with. Starting unhealthy-but-reachable means `/api/health` can explain the problem.

## Consequences

- `/api/health` is the authoritative readiness signal, not "the process is running". Alert on it (see [`../production-deployment.md`](../production-deployment.md) §5).
- **Changing config requires a restart, which drops every live session.** Never edit `config/` during a show; treat config changes as deployments.
- Config errors are caught in rehearsal rather than at the first session — provided someone actually checks `/api/health`.
```

- [ ] **Step 6: Create ADR 0005**

Create `docs/adr/0005-per-instance-session-cap.md`:

```markdown
# 0005 — Concurrency capped per instance, in memory

**Status:** Accepted, with a scale-out trap

## Context

Voice Live sessions bill per minute and consume avatar-rendering quota. Unbounded concurrency is a cost and quota incident.

## Decision

An in-memory semaphore (`SessionGate`) caps concurrent sessions at `MaxConcurrentSessions`, default **2**. Connections beyond the cap are rejected at the WebSocket upgrade.

## Alternatives rejected

- **Distributed cap in Redis or a database.** Correct for a scaled-out deployment, and unjustified infrastructure for a single-instance, single-event app.
- **No cap.** A forgotten tab or a stuck client bills indefinitely.

## Consequences

- **The cap does not survive scale-out.** N instances means N × `MaxConcurrentSessions`, silently — an operator scaling out to "add capacity" removes the control. Scale up, not out. Recorded in [`../production-deployment.md`](../production-deployment.md) §3.
- The default of 2 matches the intended deployment: one operator view and one display view.
- **Each browser tab is a session.** Opening a third tab is rejected, which surprises operators who expect tabs to share.
- There is no session timeout (finding M-01), so a slot is held until the tab closes or the app restarts. The cap bounds concurrency, not duration.
```

- [ ] **Step 7: Create ADR 0006**

Create `docs/adr/0006-region-pinned-swedencentral.md`:

```markdown
# 0006 — Region pinned to `swedencentral`

**Status:** Accepted

## Context

Voice Live features are not uniformly available across Azure regions, and the required combination is narrow.

## Decision

Deploy to `swedencentral`, the region supporting **native realtime voice, avatar rendering and agent mode together**. West Europe does not offer the full combination.

## Alternatives rejected

- **Deploy nearest the venue for latency.** Rejected: a region missing avatar or agent mode does not degrade gracefully, it fails. Feature availability beats a few milliseconds.
- **Split resources across regions.** Adds cross-region latency to the media path for no benefit.

## Consequences

- Speech is processed in the EU, which is a **compliance property**, not just a latency one. Changing region changes where attendee speech is processed — see [`../production-deployment.md`](../production-deployment.md) §10.
- Latency is bounded by the venue's distance to Sweden. Measure it during rehearsal from the actual stage network.
- A DR region must be verified to support the same feature combination before being treated as a standby.
- Some regions lack the `DOTNETCORE|10.0` runtime, needing the `LINUX_FX_VERSION` fallback documented in [`../runbook.md`](../runbook.md).
```

- [ ] **Step 8: Link the ADRs from the README**

In `README.md`, at the end of the Architecture section, add:

```markdown
The reasoning behind these choices — including what was rejected and what each decision costs — is recorded in [`docs/adr/`](docs/adr/README.md).
```

- [ ] **Step 9: Verify links**

Run: `dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true --filter "FullyQualifiedName~Maintained_markdown_has_no_broken_relative_links"`

Expected: the `docs/adr/0003-…` and `docs/adr/0006-…` forward references now resolve. Only `CONTRIBUTING.md` (Task 20) and `docs/README.md` (Task 21) remain outstanding.

- [ ] **Step 10: Commit**

```bash
git add docs/adr README.md
git commit -m "docs: add architecture decision records"
```

---

## Task 19: D-12 — threat model

Both reviews found their worst issues where an unstated trust assumption failed: that `RemoteIpAddress` is trustworthy (C-01) and that an authenticated client is benign (H-01). Writing the assumptions down is what makes them reviewable.

**Files:**
- Create: `docs/threat-model.md`
- Modify: `README.md`

- [ ] **Step 1: Create the document**

Create `docs/threat-model.md`:

```markdown
# Threat model

One page. Actors, assets, entry points, and — most importantly — the assumptions this design **trusts without verifying**. Every unstated assumption in that last table is where a security finding will come from.

**Intended deployment:** operator-attended, single-event, on a trusted network. Deviating from that invalidates most of what follows. See [Production readiness](../README.md#production-readiness).

## Assets

| Asset | Why it matters |
|---|---|
| Azure Foundry credentials | Managed identity access to a paid AI resource. **Highest-value asset.** Never leaves the server ([ADR 0001](adr/0001-server-side-credential-custody.md)). |
| The operator credential | The only thing between the internet and everything below. |
| Voice Live session capacity | Billed per minute, capped at 2 concurrent, with no timeout. Denial of service is also denial of budget. |
| What the avatar says on stage | The reputational asset. Compromise here is visible to a live audience in real time. |
| Attendee speech | Microphone audio processed in the EU. Not persisted by this app. |

## Actors

| Actor | Trust | Capability |
|---|---|---|
| Operator | Trusted | Full app access. Runs the show. |
| Authenticated user | **Trusted by the design, and this is the weak point** | Everything the operator can do, including `say`. |
| Network attacker (unauthenticated) | Untrusted | Can reach `/login`, `/api/health`, and forge headers on requests. |
| Audience member | Untrusted | Physical proximity; may be picked up by the microphone. |
| Azure Foundry | Trusted | Generates what the audience sees and hears. |

## Entry points

| Entry point | Auth | Notes |
|---|---|---|
| `GET/POST /login` | Anonymous | Credential guessing surface. Rate limiting is per-IP and header-forgeable (C-01); no antiforgery (H-02). |
| `GET /api/health` | Anonymous | Discloses configuration validity to unauthenticated callers. |
| `GET /ws/session` | Cookie + `Origin` check | Consumes a concurrency slot and starts billing. |
| `say` frame | Cookie | **Arbitrary text spoken on stage. Unconstrained (H-01).** |
| `config/*.json` | Filesystem | Anyone who can change these changes avatar behaviour. Deployment-time trust. |

## Assumptions this design trusts without verifying

**This is the section to re-read whenever the deployment changes.**

| Assumption | Status | If false |
|---|---|---|
| The network is trusted and access is limited to the event team | **Not enforced by anything.** `azd up` yields a public endpoint | Every row below becomes exploitable by anyone on the internet |
| An authenticated user is benign | **Accepted risk, deliberately** | Arbitrary avatar speech in front of an audience (H-01) |
| The client IP seen by the rate limiter is real | **False today** — forwarded headers are unvalidated | Per-IP login rate limiting is bypassable (C-01) |
| One shared credential is sufficient identity | **Accepted for this deployment shape** | No attribution, no per-person revocation ([ADR 0003](adr/0003-shared-cookie-authentication.md)) |
| The operator credential is not in source control | **Enforced** — a test fails if `appsettings.Development.json` carries an `Auth` section, and if docs publish credential literals | Public credential disclosure (C-02) |
| Config files are only writable by deployers | Deployment-time trust, unverified at runtime | Arbitrary behaviour change with no audit |
| Azure output is safe to show an audience | Trusted; no content filtering in this app | Whatever the model produces reaches the stage |

## Accepted risks

Stated so they are decisions rather than oversights:

1. **Any authenticated user can make the avatar say anything.** Accepted because the authenticated population is the event team. Unacceptable the moment that population grows — fix H-01 first.
2. **No per-operator identity or audit trail.** Accepted for a single-event deployment.
3. **No session timeout.** Accepted because sessions are attended; it is a live cost risk if that stops being true (M-01).
4. **No content filtering of avatar output.** Accepted because the model and prompt are controlled and the show is rehearsed.

## Out of scope

Azure platform security, the venue's physical security, and the endpoint security of the operator's laptop.
```

- [ ] **Step 2: Link it from the README security section**

In `README.md`, at the end of the security/trust-boundary section, add:

```markdown
Actors, assets, entry points and the assumptions this design trusts without verifying are enumerated in [`docs/threat-model.md`](docs/threat-model.md).
```

- [ ] **Step 3: Commit**

```bash
git add docs/threat-model.md README.md
git commit -m "docs: add threat model with explicit trust assumptions and accepted risks"
```

---

## Task 20: D-23 — community-health files and the licence rename

Four standard files are absent, and `licence.md` is not detected as a licence by GitHub's API, `dotnet pack` or SBOM tooling. `SECURITY.md` is the urgent one: this repository has two reviews finding Critical issues and no channel to report a vulnerability.

**Files:**
- Create: `SECURITY.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `CHANGELOG.md`
- Rename: `licence.md` → `LICENSE.md`

- [ ] **Step 1: Rename the licence with history preserved**

```bash
git mv licence.md LICENSE.md
```

- [ ] **Step 2: Fix any references to the old filename**

Run: `grep -rn "licence.md" README.md web/README.md docs/ --include='*.md'`

Update every hit to `LICENSE.md`. Expected after the update: no remaining references outside `docs/superpowers/`.

- [ ] **Step 3: Create `SECURITY.md`**

```markdown
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

Out of scope: Azure platform vulnerabilities (report to Microsoft), findings that require an already-compromised operator machine, and issues that depend on ignoring the documented deployment constraints — the app is documented as not internet-facing.
```

- [ ] **Step 4: Create `CONTRIBUTING.md`**

````markdown
# Contributing

## Prerequisites

| Tool | Version | Needed for |
|---|---|---|
| .NET SDK | 10.0+ | Server build and tests |
| Node.js | 20+ | Frontend build, type check, Playwright |
| Python 3 | any | **The Playwright suite only** — `playwright.config.ts` shells out to `python3 -m http.server`. Tests fail confusingly without it. |
| Azure CLI | latest | Local Azure auth via `DefaultAzureCredential` |
| Azure Developer CLI (`azd`) | latest | Deployment |

You also need an Azure identity holding **Cognitive Services User** and **Foundry User** on the Foundry resource. Without both, the app starts, `/api/health` reports Healthy, and every session fails with a `403`.

## Setup

```bash
git clone https://github.com/JoranBergfeld/foundry-voice-live-avatar.git
cd foundry-voice-live-avatar

az login

# Local credentials — stored outside the repository, never committed
dotnet user-secrets --project web/src/VoiceLive.Web set "Auth:Username" "<your-username>"
dotnet user-secrets --project web/src/VoiceLive.Web set "Auth:Password" "<your-password>"

dotnet run --project web/src/VoiceLive.Web
```

The frontend builds automatically as an MSBuild step. Pass `-p:SkipFrontendBuild=true` to skip it when you are only touching server code.

## Tests

```bash
# Backend — 90 tests, no frontend build
dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true

# Frontend type check
npm --prefix web/frontend run typecheck

# Playwright end-to-end — needs Python 3 on PATH
npm --prefix web/frontend test
```

Run the backend tests and the type check before opening a pull request. CI runs both.

## Documentation is tested

`web/tests/VoiceLive.Web.Tests/DocumentationTests.cs` fails the build when documentation drifts from the code: published credential literals, config-schema mismatches, `config/agent.json` keys nothing reads, unreferenced images, RBAC role names that disagree with `infra/resources.bicep`, and broken relative links.

**If a documentation test fails, the documentation is wrong** — or the code changed and the documentation did not. Fix the mismatch; do not weaken the test. Every one of these tests exists because a real defect shipped.

## Conventions

- **Commits** follow [Conventional Commits](https://www.conventionalcommits.org/): `feat:`, `fix:`, `docs:`, `test:`, `refactor:`, `chore:`.
- **Never commit credentials.** Use `dotnet user-secrets` locally and Key Vault references in Azure.
- **Update the documentation in the same commit as the behaviour change.** Documentation that describes intended-but-unimplemented behaviour is the specific defect this repository has already had to remediate at length.
- **New security-relevant behaviour** should be reflected in [`docs/threat-model.md`](docs/threat-model.md); new architectural decisions get an ADR in [`docs/adr/`](docs/adr/README.md).

## Where things live

[`docs/README.md`](docs/README.md) indexes the maintained documentation.
````

- [ ] **Step 5: Create `CODE_OF_CONDUCT.md`**

```markdown
# Code of conduct

## Our pledge

We pledge to make participation in this project a harassment-free experience for everyone, regardless of age, body size, visible or invisible disability, ethnicity, sex characteristics, gender identity and expression, level of experience, education, socio-economic status, nationality, personal appearance, race, religion, or sexual identity and orientation.

## Our standards

Examples of behaviour that contributes to a positive environment: demonstrating empathy and kindness, being respectful of differing opinions and experiences, giving and gracefully accepting constructive feedback, and focusing on what is best for the community.

Examples of unacceptable behaviour: sexualised language or imagery, trolling, insulting or derogatory comments, personal or political attacks, public or private harassment, publishing others' private information without permission, and any conduct that would reasonably be considered inappropriate in a professional setting.

## Enforcement

Report unacceptable behaviour to the repository owner through their GitHub profile. All complaints will be reviewed and investigated promptly and fairly, and the reporter's privacy and security will be respected.

Maintainers may remove, edit or reject contributions that violate this code, and may temporarily or permanently ban any contributor for behaviour they deem inappropriate.

## Attribution

Adapted from the [Contributor Covenant](https://www.contributor-covenant.org/), version 2.1.
```

- [ ] **Step 6: Create `CHANGELOG.md`**

```markdown
# Changelog

All notable changes to this project are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- Production deployment guide covering identity, secrets, capacity, cost, observability, environments, rollback, DR, networking and data handling (`docs/production-deployment.md`).
- Authoritative wire-protocol reference for `/ws/session`, including frame payload shapes (`docs/wire-protocol.md`).
- Session flow document covering the turn lifecycle, the six status indicators and per-view journeys (`docs/session-flow.md`), which also gives the previously orphaned diagrams a home.
- Six architecture decision records (`docs/adr/`).
- Threat model with explicit trust assumptions and accepted risks (`docs/threat-model.md`).
- Documentation index (`docs/README.md`); agent process history relocated to `docs/history/`.
- "Why this exists", "Non-goals", "Production readiness" and "Development" sections in the README.
- `SECURITY.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `CHANGELOG.md`.
- Automated documentation-drift tests (`web/tests/VoiceLive.Web.Tests/DocumentationTests.cs`).

### Changed
- Corrected the reconnect claim: reconnection is operator-initiated, not automatic.
- Corrected the autoplay claim: blocked autoplay ends the session; it does not show a recoverable banner.
- Described the actual Content-Security-Policy instead of calling it strict.
- Reconciled RBAC role names against the role GUIDs in `infra/resources.bicep`.
- Renamed `licence.md` to `LICENSE.md` so licence-detection tooling finds it.

### Removed
- Published development credentials from `README.md`, `web/README.md`, `docs/runbook.md`, `docs/config-schema.md` and `appsettings.Development.json`; replaced with `dotnet user-secrets` instructions.
- `agentVersion`, `conversationResumePolicy` and `groundingStrategy` from `config/agent.json` and the schema — no code reads them.
- `azure-custom` from the documented `voice.type` values — session creation always fails on it.
- Point-in-time end-to-end test evidence from the runbook.
```

- [ ] **Step 7: Verify community-health detection and links**

Run: `ls SECURITY.md CONTRIBUTING.md CODE_OF_CONDUCT.md CHANGELOG.md LICENSE.md`

Expected: all five listed.

Run: `dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true --filter "FullyQualifiedName~Maintained_markdown_has_no_broken_relative_links"`

Expected: **PASS** — the last forward reference is resolved. One link, `docs/README.md` from `CONTRIBUTING.md`, is a forward reference to Task 21; if the test fails only on that, proceed to Task 21 and re-run.

- [ ] **Step 8: Commit**

```bash
git add SECURITY.md CONTRIBUTING.md CODE_OF_CONDUCT.md CHANGELOG.md LICENSE.md README.md web/README.md docs/
git commit -m "docs: add community-health files and rename licence.md to LICENSE.md"
```

---

## Task 21: D-22 — index the documentation and separate history

91% of tracked markdown is agent process output with no index, presented alongside operational documentation with nothing distinguishing the two.

> **Note for the executing agent:** this task moves the directory containing this plan file. After Step 2, this plan lives at `docs/history/superpowers/plans/2026-08-05-documentation-alignment.md`. Re-open it there to continue.

**Files:**
- Create: `docs/README.md`
- Move: `docs/superpowers/` → `docs/history/superpowers/`

- [ ] **Step 1: Create the index**

Create `docs/README.md`:

```markdown
# Documentation

Organised by what you are trying to do ([Diátaxis](https://diataxis.fr/)). Everything listed here is **maintained and warranted accurate against the current code**. Anything under `history/` is not.

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
| [session-flow.md](session-flow.md) | You need the turn lifecycle, the six status indicators, or what a view can do. |

## Understand why

| Document | Read it when |
|---|---|
| [adr/](adr/README.md) | You want the reasoning behind an architectural choice, including rejected alternatives. |
| [threat-model.md](threat-model.md) | You are assessing security posture or changing the deployment shape. |
| [../review-merged.md](../review-merged.md) | You want the merged findings of two independent code reviews of commit `d5110dc`. |

## History — not maintained

[`history/`](history/) holds the original design specification and the agent plans and specs from the project's construction. They record intent at a point in time and are **not** kept in step with the code. Useful for archaeology, unsafe as reference.

- [history/initial-spec.md](history/initial-spec.md) — the original design specification. Its §1 rationale now lives in the [project README](../README.md#why-this-exists).
- [history/superpowers/](history/superpowers/) — implementation plans and specs from the build.
```

- [ ] **Step 2: Relocate the process history**

```bash
mkdir -p docs/history
git mv docs/superpowers docs/history/superpowers
git mv docs/initial-spec.md docs/history/initial-spec.md
```

- [ ] **Step 3: Repair references to the moved spec**

Run: `grep -rn "initial-spec.md" README.md web/README.md docs/*.md CONTRIBUTING.md SECURITY.md`

Update each hit to the new path (`docs/history/initial-spec.md` from the repo root, `history/initial-spec.md` from within `docs/`). In `docs/runbook.md` §1, also correct the framing — the spec is background, not normative reference:

```markdown
Design background (historical, not maintained): [`history/initial-spec.md`](history/initial-spec.md).
```

Fix the relative link inside `docs/history/initial-spec.md`'s status banner, which now needs one more level: `[`docs/README.md`](../README.md)` and `[project README](../../README.md#why-this-exists)`.

- [ ] **Step 4: Verify every link still resolves**

Run: `dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true --filter "FullyQualifiedName~Maintained_markdown_has_no_broken_relative_links"`

Expected: **PASS.** This is the payoff for writing the guard first in Task 1 — it proves a directory move of 16 files broke nothing.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true`

Expected: **all tests pass**, including all seven documentation tests.

- [ ] **Step 6: Commit**

```bash
git add docs CONTRIBUTING.md README.md web/README.md
git commit -m "docs: add documentation index and separate maintained docs from history"
```

---

## Final verification

- [ ] **Every documentation test passes**

Run: `dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true --filter "FullyQualifiedName~DocumentationTests"`

Expected: **8 passed, 0 failed** — link integrity, credential literals, dev-settings `Auth`, voice types, `agent.json` keys, schema keys, orphaned images, RBAC roles.

- [ ] **The full suite passes**

Run: `dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true`

Expected: all pre-existing tests still pass.

- [ ] **No false claim survives**

```bash
grep -rn "automatic reconnect\|reconnect with backoff\|strict \`Content-Security-Policy\`\|azure-custom\|groundingStrategy\|conversationResumePolicy\|grounding strategy\|resume policy\|/home/" \
  README.md web/README.md docs/*.md docs/adr/*.md CONTRIBUTING.md SECURITY.md
```

Expected: the only hits are the deliberate warning prose in `docs/config-schema.md` about the `azure-custom` trap. No hits at all in `README.md` or `web/README.md`.

- [ ] **Frontend still type-checks**

Run: `npm --prefix web/frontend run typecheck`

Expected: exit 0. Only three non-documentation files changed (`.csproj`, `appsettings.Development.json`, `config/agent.json`), but confirm.

---

## Traceability — every finding maps to a task

| Finding | Severity | Task | Resolution |
|---|---|---|---|
| D-01 automatic reconnect | High | 5 | Docs corrected to manual Reconnect |
| D-02 autoplay banner | High | 6 | Docs corrected; code fix tracked as H-05 |
| D-03 unread `agent.json` keys | High | 3, 7 | Keys deleted from config and schema; drift tests added |
| D-04 `azure-custom` | Medium | 3, 8 | Removed from schema; documented as a trap; drift test added |
| D-05 "strict" CSP | Medium | 9 | Actual policy documented verbatim |
| D-06 published credentials | Medium | 2, 10 | Purged from 4 docs + `appsettings.Development.json`; user-secrets wired up; test added |
| D-07 orphaned images | Medium | 4, 17 | All three placed in `docs/session-flow.md`; test added |
| D-08 no turn lifecycle or state model | Medium | 17 | `docs/session-flow.md` |
| D-09 views as a URL table | Low | 17 | Per-view journey table with the capacity consequence |
| D-10 no ADRs | Medium | 18 | Six ADRs plus an index |
| D-11 wire protocol triplicated / RBAC drift | Medium | 11, 16 | `docs/wire-protocol.md`; RBAC reconciled against bicep GUIDs |
| D-12 no threat model | Medium | 19 | `docs/threat-model.md` |
| D-13 the *why* is unreachable | High | 12 | Promoted to the README; spec marked historical; non-goals added |
| D-14 no test commands | Medium | 14, 20 | README Development section + `CONTRIBUTING.md` |
| D-15 hardcoded personal path | Low | 14 | `$(pwd)/config` |
| D-16 no prereq verification | Low | 14 | Version and RBAC checks; `session.sample.json` explained |
| D-17 no readiness statement | High | 13 | "Production readiness" gate before the deploy section |
| D-18 secrets and identity | High | 15 | `production-deployment.md` §1–2 |
| D-19 capacity, scale, cost | High | 15 | `production-deployment.md` §3–4 |
| D-20 day-2 operations | High | 15 | `production-deployment.md` §5–10 |
| D-21 point-in-time evidence | Low | 14 | Replaced with procedure |
| D-22 91% unindexed history | Medium | 21 | `docs/README.md`; history relocated |
| D-23 community-health files | Medium | 20 | Four files added; `LICENSE.md` renamed |
| D-24 triplicated content drifting | Medium | 11, 16 | Single authoritative locations; drift tests |

**Code findings deliberately excluded** (separate plan): C-01, C-02 code half, H-01, H-02, H-03 code half, H-04 code half, H-05, M-01…M-15, L-01…L-20. Where documentation now describes behaviour those fixes will change — Task 5 (reconnect), Task 6 (autoplay) — the task says so and names the finding.
