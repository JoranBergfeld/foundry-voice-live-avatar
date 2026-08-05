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
- Modify: `web/tests/VoiceLive.Web.Tests/DocumentationTests.cs`

**Known spec defect — resolved:** The original Step 1 table included a row:
`| Voice-only fallback when avatar capacity is unavailable | A missing video stream degrades the show; a failed session ends it. |`
This is **false**. `handleAvatarError` in `web/frontend/src/main.ts` (~line 411) calls `this.pc?.close()`, closing the single WebRTC peer connection that carries both audio and video recvonly transceivers — so audio is lost along with video. Additionally, `VoiceLiveWebSocketBridge.cs` handles only `AudioTranscriptDelta`/`Done` (transcript text); there is no `ResponseAudioDelta` case and every outbound send is `WebSocketMessageType.Text`. The correct text in `README.md:129`, `README.md:306`, `docs/runbook.md:143`, `docs/runbook.md:154` already says "no voice-only fallback". **The row was dropped.**

**Known spec defect — resolved:** Step 2's blockquote linked to `(README.md)` (relative from `docs/`), which resolves to `docs/README.md` — a file that does not exist. Fixed to `(../README.md)`.

- [x] **Step 1: Insert a "Why this exists" section**

In `README.md`, immediately after the opening paragraph and before the "How it works" section, insert:

```markdown
## Why this exists

This avatar converses **on stage with a C-level leader**, explaining company direction to a live audience, in a room that may be noisy. That single scenario, not a general chatbot use case, drives every design decision here.

**Reliability and rehearsability beat features.** Anything that can fail mid-show needs a defined behaviour and an operator control. The consequences run through the whole codebase:

| Decision | Because |
|---|---|
| Hold-to-talk turn gating is the default | An open microphone in a noisy room triggers on audience noise. The operator decides when the avatar listens. |
| Safe questions are one click away | If live Q&A stalls, the operator injects a known-good prompt rather than improvising. |
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

- [x] **Step 2: Mark the spec as historical**

At the very top of `docs/initial-spec.md`, immediately under the H1 heading, insert:

```markdown
> **Status: historical.** This is the original design specification, retained for context. It records intent at the time of writing and is **not** maintained against the current implementation. For behaviour that is warranted accurate, see the [project README](../README.md). The use case and design rationale in §1 have been promoted to the [project README](../README.md#why-this-exists).
```

- [x] **Step 3: Add guard test**

In `web/tests/VoiceLive.Web.Tests/DocumentationTests.cs`, before `Every_docs_image_is_referenced_by_maintained_markdown`, insert:

```csharp
    [Fact]
    public void Maintained_markdown_does_not_assert_a_working_voice_only_fallback()
    {
        // Voice-only fallback does not exist in this codebase:
        //   - handleAvatarError (web/frontend/src/main.ts ~line 411) calls this.pc?.close(),
        //     which closes the single WebRTC peer connection that carries both audio and video
        //     recvonly transceivers — so audio is lost along with video.
        //   - VoiceLiveWebSocketBridge.cs handles only AudioTranscriptDelta/Done (transcript
        //     text); there is no ResponseAudioDelta case and every outbound send is
        //     WebSocketMessageType.Text, which the client drops if non-string.
        // The correct text in README.md and docs/runbook.md already says "no voice-only
        // fallback" and runbook.md:143 calls it "a known gap, not a design decision".
        // This claim has been introduced, removed, and nearly reintroduced — this guard
        // prevents future drift.
        //
        // A line is flagged if it contains "voice-only fallback" (or "voice only fallback",
        // without the hyphen) but does NOT contain a negation marker in proximity to the
        // phrase. Accepted negation forms:
        //   • (no|not) immediately before the phrase, with optional markdown emphasis
        //     (*,**,`,_) between them — e.g. "no voice-only fallback",
        //     "there is no **voice-only fallback**", "not voice-only fallback"
        //   • "does not exist" or "is not implemented" within 60 characters
        //     after the phrase — "does not exist"/"is not implemented" cover direct negations;
        //   • the canonical future-aspiration form "would make ... voice-only fallback
        //     possible" — "would" is required, so a bare "voice-only fallback is possible"
        //     still fails (it asserts the feature rather than deferring it)
        // A bare "known gap" elsewhere on the same line does NOT exempt: a markdown table
        // row where independent cells share one line could disarm the guard with two common
        // words that have nothing to do with the voice-only claim.
        var root = RepoRoot;

        // Matches the trigger phrase, with or without hyphen.
        var trigger = new Regex(
            @"voice[\-\s]only\s+fallback",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        // Matches a negation in proximity to the trigger phrase:
        // Pattern A — negation before the phrase (optional markdown emphasis between them).
        // Pattern B — phrase followed by a post-phrase negation or qualifier within 60 chars
        //             ("does not exist" or "is not implemented"), or the aspirational
        //             "would make ... voice-only fallback possible" (the "would" is mandatory).
        var negated = new Regex(
            @"(no|not)\s*[\*`_]*\s*voice[\-\s]only[\*`_\s]+fallback" +
            @"|voice[\-\s]only[\*`_\s]+fallback.{0,60}(does\s+not\s+exist|is\s+not\s+implemented)" +
            @"|\bwould\b.{0,40}voice[\-\s]only[\*`_\s]+fallback.{0,20}\bpossible\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);

        var violations = MaintainedMarkdown()
            .SelectMany(rel =>
            {
                var lines = File.ReadAllLines(Path.Combine(root, rel));
                return lines
                    .Select((line, i) => (rel, line, i))
                    .Where(x => trigger.IsMatch(x.line) && !negated.IsMatch(x.line));
            })
            .Select(x => $"{x.rel} line {x.i + 1}: {x.line.Trim()}")
            .ToList();

        Assert.True(violations.Count == 0,
            "Voice-only fallback does not exist: handleAvatarError closes the peer connection " +
            "(killing both audio and video), and the server never forwards audio to the browser " +
            "(no ResponseAudioDelta case in VoiceLiveWebSocketBridge.cs). " +
            "Do not assert it as a feature or design decision. " +
            "To exempt a correct line, use a negation marker ('no', 'not', 'does not exist', " +
            "or 'is not implemented') in proximity to the phrase, or the aspirational " +
            "future-aspiration phrasing — e.g. 'no **voice-only fallback**', " +
            "'Voice-only fallback does not exist', or 'would make voice-only fallback possible'.\n  " +
            string.Join("\n  ", violations));
    }
```

- [x] **Step 4: Verify the anchor targets exist**

Run: `grep -n "^## Why this exists\|^## Non-goals\|^## Production readiness" README.md`

Expected: `Why this exists` and `Non-goals` are present. `Production readiness` is added in Task 13 — the `#production-readiness` link in Step 1 is a deliberate forward reference and will resolve then. (The link test only checks file targets, not anchors, so it stays green.)

- [x] **Step 5: Commit**

```bash
git add README.md docs/initial-spec.md web/tests/VoiceLive.Web.Tests/DocumentationTests.cs
git commit -m "docs: promote the use case, design rationale and non-goals into the README"
```

---

## Task 13: D-17 — add the production-readiness gate

The README currently walks a reader to a public HTTPS endpoint guarded by one shared password, while both reviews conclude the app is not ready for untrusted users. This is the highest-severity documentation finding: the docs invite the deployment the reviews warn against.

**Files:**
- Modify: `README.md`
- Modify: `web/src/VoiceLive.Web/appsettings.Development.json`
- Modify: `web/tests/VoiceLive.Web.Tests/DocumentationTests.cs`
- Modify: `web/tests/VoiceLive.Web.Tests/TestAppFactory.cs`

- [x] **Step 1: Insert the section immediately before "Deploy to Azure"**

In `README.md`, insert directly above the `## Deploy to Azure` heading.

**Spec row 1 was stale — corrected during implementation.** The spec said row 1 should instruct removing committed credentials and moving to `dotnet user-secrets`. An earlier task in this plan already did that: the `Auth` block was removed from `appsettings.Development.json`, `UserSecretsId` was added to the `.csproj`, and the quickstart instructs `dotnet user-secrets set`. A guard test (`Development_settings_carry_no_auth_section`) keeps it that way. Publishing the old instruction would have told readers to do something already done, destroying trust. Row 1 was rewritten to reflect what genuinely remained: **rotate the `testlab-f` Azure AI Services resource** (still named in `appsettings.Development.json`) if it was ever a real endpoint. All other rows (2–7) were confirmed still open against the current code before publishing.

**Post-commit review findings (addressed in a follow-up commit on the same branch):**

Five review findings were identified against commit `e39e998`:

- **Finding 1 (C-02 row 1, missing in-repo action):** `appsettings.Development.json` still committed the `VoiceLive:Endpoint` pointing to `testlab-f`, republishing the very hostname C-02 flags. Fix: removed the `VoiceLive` block from `appsettings.Development.json`, leaving only non-sensitive logging overrides (exactly as C-02 recommends). The quickstart already instructs `export VoiceLive__Endpoint=...`. `TestAppFactory` updated to supply a `.invalid` test endpoint so health/config tests remain green.
- **Finding 2 (C-02 row 1, unexecutable action):** "Rotate the resource" is not executable — there are no credentials on `testlab-f` to rotate (`DefaultAzureCredential`/managed identity). Dropped from the row.
- **Finding 3 (C-02 row 1, hostname published):** README re-published the `testlab-f` hostname as information disclosure. Fixed by removing the endpoint from the committed file and rewriting the row to not name the host.
- **Finding 4 (gate does not gate):** `## Deploy to Azure` was reachable via anchor/sidebar without seeing the gate. Fixed by opening the section with a blockquote back-reference to `[Production readiness](#production-readiness)` before the code block.
- **Finding 5 (false "IDs link to the finding detail." sentence):** IDs were bold text, not links. Fixed by replacing the sentence with "IDs link to the finding detail in [`review-merged.md`](review-merged.md)" and making each ID in the table a real Markdown link to the corresponding `### ID — …` anchor in `review-merged.md`.

A guard test `Development_settings_carry_no_voicelive_endpoint` was added to `DocumentationTests.cs` to keep `appsettings.Development.json` free of a committed endpoint going forward. Test count: 100 total, 98 passed, 2 pre-existing failures (unchanged).

Final table as shipped:

| # | Finding | Required action |
|---|---|---|
| 1 | [**C-02**](review-merged.md#c-02--working-credentials-committed-to-the-repository--critical) | The committed credentials (`Auth` block) have been removed and moved to `dotnet user-secrets`; `appsettings.Development.json` now carries non-sensitive logging overrides only. Operator obligation: if the Azure AI Services account named in the former endpoint was ever real and its name is sensitive, re-provision it. |
| 2 | [**C-01**](review-merged.md#c-01--login-rate-limiter-bypassable-via-spoofed-x-forwarded-for--critical) | Configure `ForwardedHeadersOptions` with known proxies and partition the rate limiter on the validated client IP; today the per-IP limiter is bypassable by a forged header. |
| 3 | [**H-01**](review-merged.md#h-01--say-control-frame-is-an-unrestricted-prompt-injection-and-cost-channel--high) | Constrain the `say` control frame to a server-side allow-list, with a length cap and per-connection rate limit. Any authenticated client can currently make the avatar speak arbitrary text on stage. |
| 4 | [**M-01**](review-merged.md#m-01--no-idle-or-absolute-session-timeout-capacity-gate-trivially-exhausted--high) | Add absolute and idle session timeouts. There is no timeout today, and the service bills per session-minute. |
| 5 | [**M-02**](review-merged.md#m-02--auth__password-stored-as-a-plaintext-app-service-setting--high) | Move `Auth__Password` out of plaintext App Service settings into a Key Vault reference. |
| 6 | [**H-02**](review-merged.md#h-02--no-csrfantiforgery-protection-on-post-login-and-post-logout--high) | Add antiforgery protection to `POST /login`. |
| 7 | [**H-05**](review-merged.md#h-05--avatar-autoplay-failure-destroys-the-session-in-unattended-views--mediumhigh) | Make blocked autoplay recoverable instead of terminating the session. |

- [x] **Step 2: Verify placement and the back-link from Task 12**

Run: `grep -n "^## Production readiness\|^## Deploy to Azure\|#production-readiness" README.md`

Expected: `## Production readiness` appears immediately before `## Deploy to Azure`, and the `#production-readiness` reference from the Non-goals section now has a matching heading.

- [x] **Step 3: Commit**

```bash
git add README.md
git commit -m "docs: add production-readiness gate before the deploy instructions"
```

- [x] **Step 4: Fix five review findings (follow-up commit)**

```bash
git add README.md web/src/VoiceLive.Web/appsettings.Development.json \
  web/tests/VoiceLive.Web.Tests/DocumentationTests.cs \
  web/tests/VoiceLive.Web.Tests/TestAppFactory.cs \
  docs/superpowers/plans/2026-08-05-documentation-alignment.md
git commit -m "fix(docs): address five review findings against e39e998 (C-02 endpoint removal, gate back-ref, ID links)"
```

---

## Task 14: D-14, D-15, D-16, D-21 — repair getting started

Four defects: no document names a test command, `web/README.md` ships a personal absolute path, the quickstart has no way to verify prerequisites, and the runbook embeds a point-in-time test result as if it were procedure.

**Files:**
- Modify: `web/README.md`
- Modify: `README.md`
- Modify: `docs/runbook.md`

- [x] **Step 1: Fix the hardcoded absolute path (D-15)**

In `web/README.md`, replace the absolute path with a repo-relative one:

```bash
ConfigDir=$(pwd)/config ASPNETCORE_URLS=http://127.0.0.1:5210 dotnet run --no-launch-profile --project web/src/VoiceLive.Web
```

Add immediately below it:

```markdown
Run this from the repository root. `$(pwd)/config` makes the path absolute and explicit. `dotnet run` sets the app's working directory to the **project** directory (`web/src/VoiceLive.Web`), not the invocation directory, so a relative path such as `./config` resolves under the project directory and will not find the config files. Use `$(pwd)/config` (absolute, from the repo root) or `../../../config` (relative to the project directory) instead.
```

> **Regression correction — Step 1:** The commit removed `--no-launch-profile` and changed `web/src/VoiceLive.Web` to `src/VoiceLive.Web`. Both were regressions: without `--no-launch-profile` the launch profile overrides `ASPNETCORE_URLS` and binds port 5280 instead of 5210; `src/VoiceLive.Web` does not resolve from the repo root. The pre-commit command was correct on both points.
>
> The explanatory note also contained a false claim. Verification from the reviewer proved that `ConfigDir` is resolved against the app's working directory, which `dotnet run` sets to the **project** directory, not the invocation directory. `ConfigDir=./config` from the repo root therefore fails with `config/session.json: file not found`. The note has been corrected to state the true behaviour.

- [x] **Step 2: Verify the path is gone**

Run: `grep -rn "/home/" README.md web/README.md docs/*.md`

Expected: **no output.** ✓ confirmed.

- [x] **Step 3: Add prerequisite verification to the README quickstart (D-16)**

In `README.md`, at the start of the quickstart section, insert:

```bash
dotnet --version   # 10.0 or later
node --version     # 24 or later
python3 --version  # required by the Playwright suite's static file server
az account show --query '{sub:name, user:user.name}' -o table
az role assignment list --assignee "$(az ad signed-in-user show --query id -o tsv)" \
  --all --query "[].roleDefinitionName" -o tsv
```

> **Spec deviation — Step 3 `az` command:** The spec omitted `--all`. `az role assignment list` without `--all` scopes to the subscription only; the Bicep assigns `Cognitive Services User` and `Foundry User` at the Foundry account and project resource scopes. Running the command without `--all` returned empty output on this subscription. `--all` was added. Verified by running both forms.
>
> **Spec deviation — Step 3 Node version:** The spec said `# 20 or later`; the README prerequisites list "Node.js 24" and the runtime is v24.14.1. Changed to `# 24 or later`.

- [x] **Step 4: Explain `session.sample.json` (D-16)**

Confirmed excluded from publish in `.csproj` line 26. Done.

- [x] **Step 5: Add a Development section pointing at the tests (D-14)**

Done. Section inserted directly before `## Production readiness`. Heading order confirmed: `## Development` (144) → `## Production readiness` (159) → `## Deploy to Azure` (179).

- [x] **Step 6: Remove point-in-time test evidence from the runbook (D-21)**

Done. Replaced past-tense result with procedure.

- [x] **Step 7: Verify the documented commands actually work**

`dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true`: 98 passed / 2 failed / 100 total. Failures: `Every_docs_image_is_referenced_by_maintained_markdown` and `Maintained_markdown_has_no_broken_relative_links` (six permitted entries). ✓

`npm --prefix web/frontend run typecheck`: exit 0. ✓

Playwright uses `python3 -m http.server` (confirmed in `playwright.config.ts`). Python 3.12.3 present. Note: the Playwright suite is a frontend regression suite with mocked transport; it does not verify the Azure avatar path end to end. Runbook §7 updated to reflect this.

- [x] **Step 8: Commit**

```bash
git add README.md web/README.md docs/runbook.md
git commit -m "docs: fix getting-started gaps — test commands, prereq checks, portable paths"
```

- [x] **Step 4 (follow-up): Explain `session.sample.json` in the config directory listing (D-16)**

In the `README.md` config-directory listing, replace the `session.sample.json` description with:

```markdown
- `session.sample.json` — a reference copy of `session.json`, excluded from publish. Copy it over `session.json` to return to known-good settings after experimenting, and diff against it when a config change causes a startup validation failure.
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

**Never** set the operator password with `azd env set AUTH_PASSWORD <password>` for a production deployment. That lands the sole credential in plaintext App Service configuration, readable by anyone with Contributor or Website Contributor on the resource (any principal holding `Microsoft.Web/sites/config/list/action`).
Use a Key Vault reference instead:

```bash
az keyvault secret set --vault-name <vault> --name auth-password --value "<password>"

az webapp config appsettings set --name <app> --resource-group <rg> --settings \
  "Auth__Password=@Microsoft.KeyVault(VaultName=<vault>;SecretName=auth-password)"
```

The web app's system-assigned managed identity needs **Key Vault Secrets User** on the vault. Verify resolution before the event — a failed reference surfaces as the literal `@Microsoft.KeyVault(...)` string becoming the password, so **sign in successfully after every secret change**.

**Rotation.** Rotate after every event and whenever anyone with the credential leaves the team. Rotation is a secret update. Sessions signed in with the old cookie remain valid because the cookie is a self-contained Data Protection payload, and neither a password nor a username change revokes any active session. **There is no immediate revocation mechanism in the application.** To force all sessions to drop, destroy the key ring (`%HOME%/ASP.NET/DataProtection-Keys` via Kudu/SSH) and restart.

**Key Vault reference ordering.** The `Auth__Password` application setting is also written on every `azd provision`, sourced from `AUTH_PASSWORD`. Any `azd up` or `azd provision` run after setting the Key Vault reference will overwrite it. Re-apply the Key Vault reference and re-verify sign-in after every provision. For a first deployment, `AUTH_PASSWORD` must be non-empty so the app starts with a valid credential; replace it with the Key Vault reference immediately after provision.

**Never** commit credentials. `appsettings.Development.json` no longer carries an `Auth` section, and a test enforces that.

## 3. Capacity and quota

Three independent limits, in the order you will hit them:

| Limit | Value | Behaviour when exceeded | Where to change |
|---|---|---|---|
| Concurrent app sessions | `MaxConcurrentSessions`, default **2** | New connections are rejected at the gate | `VoiceLive__MaxConcurrentSessions` App Service application setting (overrides the `appsettings.json` default) |
| Avatar rendering quota | Per Azure AI Foundry resource | `avatar_service_resource_exhausted`; the peer connection closes and **both audio and video are lost — there is no voice-only fallback** | Azure quota request |
| App Service instance | B1, single instance | CPU saturation and dropped audio | App Service plan |

**The concurrency gate is per-instance and in-memory.** Scaling out to N instances does not share the cap — it multiplies it to N × `MaxConcurrentSessions`, silently. **Do not scale out to increase capacity.** Scale up instead, and raise `MaxConcurrentSessions` deliberately, having tested the instance can carry the load.

**Each browser tab is a session.** An operator view plus a display view is two sessions — the entire default budget. Plan the slot count against the number of tabs you will actually open, plus one spare for a mid-show reconnect.

**Request avatar quota before the event, not on the day.** Quota approval is not instant, and the failure mode is a media-plane failure — the peer connection closes and the avatar goes dark, while the WebSocket session, microphone and transcripts keep running.

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
| Capacity rejections | `voicelive.active_sessions` sustained at `MaxConcurrentSessions` | Someone opened one tab too many (gate-rejection metric is not yet emitted — M-01 remediation) |
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

// Avatar quota exhaustion — both audio and video are lost when this fires
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
az webapp log deployment list --name <app> --resource-group <rg> \
  --query "[].{id:id, time:received_time, active:active}" -o table

# Roll back by re-deploying a retained artifact (deployment id is for correlation/audit only;
# no slot-swap rollback is available — the B1 plan cannot host staging slots)
az webapp deploy --name <app> --resource-group <rg> --src-path <previous.zip> --type zip
```

**Prepare a rollback before the event:** keep the last known-good published artifact, and record its deployment id in the event runbook. Mid-show is not when you discover the artifact is gone.

**Configuration rollback is separate.** Config is read from `config/` **at startup only** and there is no hot reload (finding L-20). Changing config requires an app restart, which drops every live session. **Never edit config during a show.** Treat `config/` changes as deployments: change, restart, re-verify `/api/health`, re-run the smoke test.

## 8. Business continuity

The whole project exists to serve one high-stakes live moment, so plan for the region being degraded 30 minutes before it.

| Scenario | Prepared fallback |
|---|---|
| Foundry region degraded | Pre-provision a second `azd` environment in an alternate region **that supports native realtime voice, avatar and agent mode**. Verify it during rehearsal — an untested standby is not a standby. |
| Avatar quota exhausted | `handleAvatarError` closes the peer connection; **both audio and video are lost**. This is a media-plane failure — the WebSocket session, microphone and transcripts keep running. Prepare a full fallback plan (pre-recorded segment or static slides), agree the abort call, and brief the speaker beforehand so the failure is not a surprise on stage. |
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

**Shipped — spec defects corrected:**

- **Defect 1 (§3 voice-only fallback):** Spec said avatar quota exhaustion "falls back to voice-only". False — `handleAvatarError` in `main.ts` closes the peer connection, ending both audio and video. Corrected to: "both audio and video are lost — there is no voice-only fallback".
- **Defect 2 (§8 business-continuity avatar row):** Spec said "Voice-only mode already degrades automatically." False for the same reason. Corrected: `handleAvatarError` closes the peer connection; both audio and video are lost. This is a media-plane failure — the WebSocket session, microphone and transcripts keep running.
- **Defect 3 (§3 `MaxConcurrentSessions` location):** Spec said `config/session.json`. False — the setting is bound by `VoiceLiveOptions` and lives in `appsettings.json`; in Azure it is overridden via `VoiceLive__MaxConcurrentSessions` App Service application setting.
- **Additional (§2 rotation):** Spec claimed changing `Auth:Username` invalidates cookies. False — the auth guard checks only `IsAuthenticated`, never the username against config. Corrected: there is no immediate revocation mechanism; destroying the Data Protection key ring and restarting is the only way to drop all active sessions.
- **Additional (§6 environments table):** `` `rehearsal` `` backtick-wrapped tripped the credential-literals guard. Changed to plain "rehearsal".

**Tests: 98 passed / 2 failed / 100 total.** Failures remain `Every_docs_image_is_referenced_by_maintained_markdown` and `Maintained_markdown_has_no_broken_relative_links` (broken-link set matches expected).

---

## Task 16: D-11, D-24 — one authoritative wire-protocol reference ✅

Created `docs/wire-protocol.md` as the authoritative reference for `/ws/session`, replaced the endpoint/frame tables in `web/README.md` with a single link, and added a pointer in `README.md` after its existing frame summary tables.

**Files modified:** `docs/wire-protocol.md` (created), `web/README.md`, `README.md`

- [x] **Step 1: Create `docs/wire-protocol.md`**

  Create the document with the endpoints table, lifecycle narrative, browser→server frame table, server→browser frame table, `ReadyConfig`/`ClientConfig`/`IceServer` field tables, and per-view frame restrictions table. Verify all frame shapes, field names, and semantics against source before committing.

- [x] **Step 2: De-duplicate `web/README.md`**

  Replace the endpoint table and frame vocabulary with a single link to `docs/wire-protocol.md`.

- [x] **Step 3: Add pointer in `README.md`**

  After the existing server→browser frame summary, add a sentence pointing at `docs/wire-protocol.md` as authoritative.

- [x] **Step 4: Commit**

  ```bash
  git add docs/wire-protocol.md web/README.md README.md
  git commit -m "docs: add authoritative wire-protocol reference (D-11, D-24)"
  ```

**Discrepancies found and corrected (spec vs. source):**

1. **Discriminator field**: The spec's payload examples throughout used `"type"` as the JSON discriminator. The actual code — both TypeScript (`frame.t`) and C# (`tProp.GetString()` from property `"t"`) — use `"t"`. All payload shapes in the shipped document use `"t"`. Added a prominent note at the top of the document.

2. **`avatar-offer` is unconditional**: The spec said "Once, after `ready`, when the avatar is enabled." Code in `onReady` calls `negotiateAvatar` unconditionally for all views; `avatar-offer` is always sent. Updated wording to "Unconditionally, once, immediately after `ready`, by all views including display."

3. **Display view behaviour**: The spec lifecycle step 5 implied `avatar-offer` was conditional on avatar being enabled. Verified that `prepareMicrophone` and `wireInteractiveControls` both `return` immediately for non-interactive views; display view opens WebSocket, receives `ready`, does WebRTC negotiation, but cannot send turn frames. Added a "Per-view frame restrictions" table.

4. **`avatar-error` semantics**: The spec said "voice-only fallback" and "voice continues". The actual `handleAvatarError` closes the `RTCPeerConnection`; both audio and video are lost. The shipped document states: "There is no voice-only fallback." Correct per `main.ts` comment and task constraints.

5. **Missing endpoint `/api/config`**: `Program.cs` maps `GET /api/config` (cookie-authenticated, returns browser-safe config as the `ClientConfig` record). The spec omitted it; added to the endpoints table with the full nine-field schema.

6. **Stale verification claim**: The spec said `d5110dc`; current HEAD of `docs-alignment` is `d657e86`. Replaced with accurate commit reference.

7. **`tool` phase values not enumerated**: The spec said only `phase: string`. The bridge emits `"args"`, `"done"`, `"list"`, `"list-done"`, `"list-failed"`. Listed in the shipped document. `name` and `callId` are always serialised (nulls included per `JsonSerializerDefaults.Web`); `name` is non-null only for phase `"done"`.

8. **`activeMode` values**: The spec listed `"gated"`, `"open"`, `"hybrid"`. Server config uses `"gated"`, `"open-mic"`, `"hybrid"` (verified in `ServerSessionConfig.cs` and `prepareMicrophone`). Fixed. Note: `activeMode` in the `ready` payload comes from `config.TurnTaking.ActiveMode`; `SessionModeResolver` resolves `"model"`/`"agent"` mode, not turn-taking mode.

9. **`agentName` is required, never empty**: `ServerSessionConfig.cs:104` calls `RequireServer(agent.AgentName, ...)` with no mode guard; the app will not start without it in either mode. The `ReadyConfig` row documents it as `string` with no nullability caveat.

10. **`IceServer` shape**: Added explicit `IceServer` table (`urls: string[]`, `username?: string`, `credential?: string`) to match `BuildIceServers` output.

---

## Task 17: D-07, D-08, D-09 — session flow, state model and view journeys

**SHIPPED.** Created `docs/session-flow.md` and linked from `README.md` after the Session startup sequence diagram. All three orphaned images are now referenced. Test result: **100 passed / 1 failed / 101 total**. Remaining failure is `Maintained_markdown_has_no_broken_relative_links` with only the six permitted broken links.

**Spec errors corrected (six):**

1. **Voice-only fallback claim (three occurrences)** — spec asserted that `webrtc` failure is survivable and that voice may still work without video. Source: `handleAvatarError` in `main.ts` calls `this.pc?.close()`, closing the peer connection that carries both transceivers; both avatar audio and video are lost. There is no voice-only fallback. Document matches `docs/wire-protocol.md` and `docs/runbook.md §9` wording: "There is no voice-only fallback." The "Diagnostic shortcut" paragraph was removed entirely.

2. **Inverted transcript semantics** — spec said interim frames are "replaced as recognition improves". Source: `views.ts:81-82`: `const transcriptText = final ? text : liveText[role] + text;` — interim chunks **append**; the final frame (which carries the complete transcript) **replaces**. Corrected to match `docs/wire-protocol.md`.

3. **`open` literal** — spec listed mode as `open`. Source: `config/turntaking.json` and `main.ts:330` show the literal is `open-mic`. Corrected to `open-mic` throughout.

4. **`barge-in` and `say` availability** — spec implied these were available from the landing view. Source: `main.ts:219,225` gates `barge-in` on `view.stopButton` and `say` on `view.repeatButton`/`view.safeQuestionButtons`; the landing view has none of these. Corrected: marked operator-view only, consistent with `wire-protocol.md` per-view table.

5. **Status channel healthy values** — spec used fictional values (`idle/active`, `idle/detected`, `connected`). Source: `main.ts:406` and actual `setStatus` calls throughout. Documented the real string values emitted by the code.

6. **Decision points diagram caption** — spec said "avatar enabled vs. voice-only". Removed; replaced with accurate description (model mode vs. agent mode, avatar capacity, connection outcomes).

**Additional corrections:**
- Dropped unverifiable claim that "`end-turn` without `start-turn` is a no-op" (client guards with `if (!this.streamingMic) return`).
- `MaxConcurrentSessions` default of 2 verified in `appsettings.json` and `VoiceLiveOptions.cs`.
- `SessionGate` singleton `SemaphoreSlim` verified in `SessionGate.cs` and `Program.cs`.
- Autoplay wording taken verbatim from `runbook.md §7`.
- README link inserted after the sequence diagram (the spec's "How it works" section is actually titled "Session startup").

**Files:**
- Create: `docs/session-flow.md`
- Modify: `README.md`

- [x] **Step 1: Confirm the images are still there**

Run: `ls docs/images/`

Expected: `voice_live_decision_points.png`, `voice_live_prewarm_connection_flow.png`, `voice_live_single_turn_flow.png`.

- [x] **Step 2: Create the document**

Create `docs/session-flow.md`:

````markdown
# Session flow and state

How a session starts, how a turn runs, what the status indicators mean, and what each view can do. For frame payloads see [`wire-protocol.md`](wire-protocol.md).

## Connection flow

![Voice Live connection and pre-warm flow](images/voice_live_prewarm_connection_flow.png)

The browser holds no Azure credential at any point. The server acquires the token, opens the upstream session, and only then tells the browser it is `ready`. Avatar media is negotiated afterwards and flows directly browser↔Azure over WebRTC.

## A single turn

![Voice Live single turn flow](images/voice_live_single_turn_flow.png)

> **Image note:** the diagram's "WS deltas" box is inaccurate. The WebSocket carries **text frames only** (transcripts, state, tool and error events); all avatar audio and video arrive over the WebRTC media plane.

In **gated** mode (the shipped default) a turn is explicitly bracketed by the operator:

1. Operator presses **Hold to talk** → client sends `start-turn` and begins streaming microphone audio.
2. Binary PCM16 frames flow while the button is held. Turn detection is `NoTurnDetection` in gated mode: **no `speech-started`/`speech-stopped` events are emitted**.
3. Operator releases → client sends `end-turn` and stops streaming audio. `InputAudioTranscription` is not set in gated mode, so **no `user-transcript` frames are emitted**.
4. The model responds: `agent-transcript` frames stream in, `avatar-speaking` fires when audio playback begins, avatar video and audio arrive over the WebRTC media plane.
5. `avatar-idle` then `response-done` close the turn.

In **`open-mic`** and **`hybrid`** modes the server emits `speech-started`/`speech-stopped` VAD events and `user-transcript` frames. `final: false` frames carry a delta to **append**; `final: true` carries the complete transcript and **replaces** it.

**Safe questions** (operator view only) skip the turn steps: clicking one sends a single `say` frame and the flow resumes at step 4.

**Barge-in** (operator view only) sends `barge-in` during model speech to interrupt the avatar.

### Turn-taking modes

| Mode | How turns start | `start-turn` / `end-turn` sent? | VAD segments turns? |
|---|---|---|---|
| `gated` | Hold to talk (default) | Yes, by the operator | No |
| `open-mic` | Automatically on `ready` | No | Yes — Azure semantic VAD |
| `hybrid` | Automatically on `ready` | No | Yes — Azure semantic VAD |

The active mode is reported in the `ready` frame as `activeMode`.

### Rules and edge cases

- **Wait for `ready`.** The client must not send anything before `ready`; the server does not enforce this gate.
- **Mute** is available on the landing view in `open-mic`/`hybrid` modes only. **There is no mute control on the operator view.**
- **Barge-in outside avatar speech** is harmless but pointless — there is nothing to interrupt.
- **`barge-in` and `say` are operator-view only.**

## Decision points

![Voice Live decision points](images/voice_live_decision_points.png)

Three decision paths: barge-in (check mode → cancel or ignore), repeat request (re-synthesize), and connection drop. **The diagram's connection-drop branch depicting automatic freeze-and-retry with fallback video is aspirational, not shipped** — the real behaviour is full teardown plus a manual Reconnect button.

## Status channels

The operator view exposes six independent status channels. The landing view surfaces only `connection` and `webrtc` in a transient pill. The display view collapses `connection`, `webrtc`, and `avatar` into a single status string.

| Channel | Representative values | Meaning when unhealthy |
|---|---|---|
| `connection` | `ready` (healthy); `connecting`; `disconnected` | WebSocket to the app is down. **Reconnect** is the only recovery. |
| `webrtc` | `connected` (healthy); `failed`; `avatar disabled (capacity)` | Media-plane failure. Both avatar audio and video are lost. **There is no voice-only fallback.** |
| `microphone` | `ready` / `live` (healthy); `muted` | Microphone failure is **fatal** — `disconnect()` is called, tearing down the entire session. **Reconnect** is the only recovery. `muted` (landing view, non-gated only) is non-fatal. |
| `turn` | `gated: hold to talk` / `open-mic: streaming continuously` (idle); `recording gated turn` (active) | Stuck on `recording gated turn` → release and re-press Hold to talk. |
| `speech` | `started` / `stopped` | **Only emitted in `open-mic`/`hybrid`** — gated mode uses `NoTurnDetection` and this channel never leaves its initial state. That is normal, not a fault. |
| `avatar` | `speaking` / `idle` (healthy); `unavailable` | `unavailable` means an `avatar-error` frame was received. |

## The three views

All three are the same app shell, selected by query string, and **each open tab is its own session consuming one concurrency slot**. `SessionGate` is a singleton `SemaphoreSlim` backed by `VoiceLiveOptions.MaxConcurrentSessions` (default `2`, configured in `appsettings.json`).

| View | URL | Microphone | Controls | Intended screen |
|---|---|---|---|---|
| Landing | `/` | Yes | Hold to talk (gated only); mute toggle (non-gated only, same button); Reconnect (on disconnect); ⚙ gear to operator view | Setup and testing |
| Operator | `/?view=operator` | Yes | Hold to talk (gated only), safe questions, barge-in, all six status channels, Reconnect. **No mute control.** | The operator's laptop, never visible to the audience |
| Display | `/?view=display` | **No** | Avatar video only; Reconnect appears on disconnect | The stage screen |

**Two consequences worth planning for:**

- The display view has no microphone and no interaction affordance, yet a browser will still block autoplay until the page receives a user gesture. **Always click into the display screen once before the audience arrives.** Recovery: click **Reconnect**. See [`runbook.md`](runbook.md) §7.
- Reconnection is operator-initiated; there is no automatic reconnect. An unattended display screen that disconnects stays disconnected until someone clicks Reconnect.
````

- [x] **Step 3: Link it from the README**

```markdown
The turn lifecycle, the six status indicators and what each view can do are documented in [`docs/session-flow.md`](docs/session-flow.md).
```

- [x] **Step 4: Run the orphaned-image test**

Run: `dotnet test web/VoiceLive.Web.sln -p:SkipFrontendBuild=true --filter "FullyQualifiedName~Every_docs_image_is_referenced"`

Expected: **PASS** — all three images are now referenced.

- [x] **Step 5: Commit**

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

The server holds all Azure credentials. It acquires a token via `DefaultAzureCredential` — a managed identity in Azure, developer credentials locally — opens the upstream Voice Live session itself, and relays control frames and audio uplink (browser → Azure) over `/ws/session`. The server never sends audio to the browser; avatar audio and video reach the browser exclusively over WebRTC. No token, key or connection string is ever sent to the browser.

## Alternatives rejected

- **Browser-minted ephemeral tokens.** Still puts an Azure-scoped credential in a context the operator's browser extensions, the venue network and anyone with the laptop can reach. The blast radius of a leak is the Foundry resource, not this app.
- **API keys in config.** Same exposure, without expiry.

## Consequences

- The Foundry resource is never directly reachable by a client. Compromising the browser yields an app session, not Azure access.
- The server is on the audio path for the uplink, so it must be sized for concurrent audio relay.
- Local development needs a signed-in developer identity with the right roles — the most common first-run failure. See [`../runbook.md`](../runbook.md) §4.
```

- [ ] **Step 3: Create ADR 0002**

Create `docs/adr/0002-direct-webrtc-media-plane.md`:

```markdown
# 0002 — Avatar media bypasses the server

**Status:** Accepted

## Context

The avatar produces a video and audio stream. Relaying it through the app server, as the control plane is relayed, would be architecturally uniform.

## Decision

Avatar media uses WebRTC **directly between the browser and Azure**. The server relays only the SDP offer/answer and ICE configuration; once negotiated, media never touches the app. Frame payload shapes are documented in [`../wire-protocol.md`](../wire-protocol.md).

## Alternatives rejected

- **Server-relayed media.** Adds a hop of latency to a live stage performance and makes the B1 App Service instance a video relay, which it cannot do at acceptable quality.

## Consequences

- Lowest achievable latency, and video quality is independent of app instance size — important, because the whole point is a believable on-stage presence.
- **The venue's network must reach Azure directly over WebRTC.** Restrictive venue firewalls break the avatar while leaving the control plane working, which presents as a working session with no avatar video or audio. Test from the actual stage position on the actual network.
- The server cannot observe, record or moderate avatar output. What Azure renders is what the audience sees.
- `avatar-error` is a **media-plane failure**, not a session failure. `handleAvatarError` closes the `RTCPeerConnection`; both avatar video **and audio** are lost because both `recvonly` transceivers ride that single peer connection. The WebSocket, concurrency slot, microphone capture and transcripts survive, but the room receives no avatar output. The failure mode is a working session with no avatar video or audio — the operator must invoke a fallback plan. A control-plane `error` ends the session entirely.
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

- **This is the entire authorization model.** The authorization middleware (`Program.cs`) checks only `ctx.User.Identity?.IsAuthenticated` — every authenticated user reaches every endpoint, including `say`, which puts arbitrary text in the avatar's mouth on stage (finding H-01).
- No audit trail attributable to a person. "Who made it say that" has no answer.
- **Revoking a session is not possible by changing credentials.** The authorization check does not re-validate credentials after sign-in. Changing the shared password or username does not invalidate live cookies, and because the 8-hour expiry is **sliding**, an active tab renews the cookie on each request and never ages out on its own. The only revocation path is destroying the ASP.NET Data Protection key ring. On App Service, the key ring persists to `%HOME%\ASP.NET\DataProtection-Keys` (a network-backed share), so restarting the app does **not** revoke sessions; only destroying the key ring does.
- Consequently the app **must not be left internet-facing** — but nothing enforces that. `azd up` provisions a public App Service with no IP restrictions or VNet integration, so out of the box the shared password is the only access control. Adding App Service access restrictions is the operator's responsibility; see [`../production-deployment.md`](../production-deployment.md) §9.
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

Config is read and validated **once, at startup**. A `WebConfigValidationException` during loading is caught; the app starts and reports the problem through `/api/health` (503 Unhealthy) rather than crashing. There is no file watcher and no reload endpoint.

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

An in-memory semaphore (`SessionGate`) caps concurrent sessions at `MaxConcurrentSessions`, default **2**, bound from `VoiceLiveOptions` (ASP.NET configuration). Connections beyond the cap have their WebSocket handshake accepted, then immediately receive a text error frame (`"The server is at capacity. Try again shortly."`) and the connection closes. Override the default via the `VoiceLive__MaxConcurrentSessions` app setting.

## Alternatives rejected

- **Distributed cap in Redis or a database.** Correct for a scaled-out deployment, and unjustified infrastructure for a single-instance, single-event app.
- **No cap.** A forgotten tab or a stuck client bills indefinitely.

## Consequences

- **The cap does not survive scale-out.** N instances means N × `MaxConcurrentSessions`, silently — an operator scaling out to "add capacity" removes the control. Scale up, not out. Recorded in [`../production-deployment.md`](../production-deployment.md) §3.
- The default of 2 matches the intended deployment: one operator view and one display view.
- **Each browser tab is a session.** Opening a third tab is rejected, which surprises operators who expect tabs to share.
- There is no session timeout (finding M-01 — "No idle or absolute session timeout; capacity gate trivially exhausted"), so a slot is held until the tab closes or the app restarts. The cap bounds concurrency, not duration.
```

- [ ] **Step 7: Create ADR 0006**

Create `docs/adr/0006-region-pinned-swedencentral.md`:

```markdown
# 0006 — Region pinned to `swedencentral`

**Status:** Accepted

## Context

Voice Live features are not uniformly available across Azure regions, and the required combination is narrow.

## Decision

Deploy to `swedencentral`, the region supporting **native realtime voice, avatar rendering and agent mode together**. This is pinned as the default in `infra/main.bicep`. West Europe does not offer the full combination.

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

- [x] **Step 10: Commit**

```bash
git add docs/adr README.md
git commit -m "docs: add architecture decision records"
```

**Spec errors corrected during execution:**

1. **ADR 0001:** "relays control and audio frames to the browser" was wrong. `PumpVoiceLiveUpdatesAsync` has no `ResponseAudioDelta` case and every send is `WebSocketMessageType.Text`. Rewritten to: "relays control frames and audio uplink (browser → Azure); avatar audio and video reach the browser exclusively over WebRTC."

2. **ADR 0002:** Two errors corrected:
   - "degrades to voice-only" — `handleAvatarError` calls `this.pc?.close()`, and both video and audio transceivers ride that one peer connection. Both avatar video and audio are lost. Rewritten to describe a media-plane failure with no audio fallback.
   - "presents as a working session with no video" — it is no video and no audio. Fixed.

3. **ADR 0003:** Revocation consequences were understated. Authorization surface is only `IsAuthenticated` (`Program.cs:93-110`). Changing credentials does not revoke live sessions. Data Protection is unconfigured → key ring at `%HOME%\ASP.NET\DataProtection-Keys` (network-backed) → restart does not revoke; only destroying the key ring does. Strengthened accordingly.

4. **ADR 0004:** Confirmed consistent with settled text. Added explicit 503 status code. `WebConfigValidationException` is caught; app starts and reports via `/api/health` (503 Unhealthy).

5. **ADR 0005:** Two errors corrected:
   - "rejected at the WebSocket upgrade" — the WebSocket IS accepted first; then a text error frame is sent and the connection closes. Fixed to describe post-handshake rejection.
   - `MaxConcurrentSessions` default comes from `VoiceLiveOptions` (ASP.NET configuration), not `config/`. Override via `VoiceLive__MaxConcurrentSessions`. Fixed.
   - M-01 full title verified: "No idle or absolute session timeout; capacity gate trivially exhausted".

6. **ADR 0006:** `swedencentral` default confirmed at `infra/main.bicep:5`. `DOTNETCORE|10.0` confirmed at `infra/main.bicep:24`. Added `infra/main.bicep` reference to the Decision section.

---

## Task 19: D-12 — threat model ✅ SHIPPED

Both reviews found their worst issues where an unstated trust assumption failed: that `RemoteIpAddress` is trustworthy (C-01) and that an authenticated client is benign (H-01). Writing the assumptions down is what makes them reviewable.

**Files:**
- Created: `docs/threat-model.md`
- Modified: `README.md`

**Spec errors corrected:**

1. **Entry-points table was incomplete.** The spec omitted three endpoints present in `docs/wire-protocol.md` (the authoritative reference):
   - `GET /` (cookie-protected, app shell) — added
   - `POST /logout` (anonymous, H-02 target) — added; spec inconsistently cited H-02 for `/login` but omitted `/logout` which the finding explicitly names
   - `GET /api/config` (cookie-protected, returns browser-safe config) — added; confirmed at `Program.cs:119-122`

2. **"Enforced" row named the wrong thing.** Spec said "a test fails if…" without naming the tests. The actual test names are `Development_settings_carry_no_auth_section` and `Maintained_markdown_publishes_no_credential_literals`. These are now cited accurately.

3. **Attendee-speech privacy claim was imprecise.** Spec said "Not persisted by this app" without noting the gated-mode nuance. Verified: default `activeMode = "gated"` sets `manualTurn: true`, which causes `UsesTurnDetection()` to return false, so `InputAudioTranscription` is never configured — no user transcripts are produced at all by default. This is materially relevant to a privacy claim and is now stated accurately.

4. **ADR 0003 cookie-revocation nuance omitted.** The "shared credential" row now notes that changing the password or username does **not** revoke live sessions (8-hour sliding cookie), and that restarting on App Service does not revoke either — only destroying the key ring does. Links to ADR 0003.

5. **Bicep re-provision clobbers Key Vault references.** `infra/resources.bicep:89` writes `Auth__Password` as plaintext on every `azd up`, which clobbers any Key Vault reference set between provisions. Added as a row in the assumptions table, referencing M-02.

6. **Scale-out multiplies the concurrency cap.** Per ADR 0005, `MaxConcurrentSessions = 2` is per-instance. Added as accepted risk #4.

7. **README link placement.** The spec said "security/trust-boundary section". `README.md` does have `### Authentication and trust boundaries` — the earlier note that it did not was wrong. Pointers were added in both places: at the end of `## Production readiness`, and under `### Authentication and trust boundaries`, which is the section a reader searching for the trust boundary will find first.

8. **`#production-readiness` anchor verified.** The heading `## Production readiness` in `README.md` produces anchor `#production-readiness`. Confirmed by reading the file.

9. **`Origin` check on `/ws/session` verified.** `Program.cs:137` calls `OriginAllowed(context, opt.Value.AllowedOrigins)`. Non-browser clients with no `Origin` header are allowed through (lines 178-179). Documented accurately.

- [x] **Step 1: Create the document**

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
| Attendee speech | Microphone audio sent to Azure (EU region by default). In the default **gated** mode (`turntaking.json: activeMode = "gated"`), `manualTurn` is `true`, so `InputAudioTranscription` is never configured and **this app emits no `user-transcript` frames** — transcription is only active when a mode with turn detection is selected. That is a statement about this app only: the audio is still streamed to and processed by Azure regardless, and **Azure-side retention and abuse monitoring are governed by your Foundry resource, not by this application** — see [`production-deployment.md` §10](production-deployment.md#10-data-handling-and-privacy), which also covers the recording-notice obligation. |

## Actors

| Actor | Trust | Capability |
|---|---|---|
| Operator | Trusted | Full app access. Runs the show. |
| Authenticated user | **Trusted by the design, and this is the weak point** | Everything the operator can do, including `say`. |
| Network attacker (unauthenticated) | Untrusted | Can reach `/login`, `/logout`, `/api/health`, and forge headers on requests. |
| Audience member | Untrusted | Physical proximity; may be picked up by the microphone. |
| Unattended display browser | **Trusted by the design, and rarely attended** | The display view requires the auth cookie, so the machine driving the venue screen holds a live authenticated session in a public space. The cookie is 8-hour **sliding** and no password change revokes it, so anyone who reaches that keyboard inherits full app access — including `say`. Treat the display machine as a credential. |
| Azure Foundry | Trusted | Generates what the audience sees and hears. |

## Entry points

Authoritative source: [`docs/wire-protocol.md`](wire-protocol.md). Any disagreement between the two documents is a bug here.

| Entry point | Auth | Notes |
|---|---|---|
| `GET /` | Cookie | Application shell (operator, display, landing views). Unauthenticated requests redirect to `/login`. |
| `GET /login` | Anonymous | Sign-in form. |
| `POST /login` | Anonymous | Credential submission. Rate limiting is per-IP and header-forgeable (C-01); no antiforgery (H-02). |
| `POST /logout` | **Anonymous** | Clears the auth cookie. Deliberately on the anonymous allow-list (`Program.cs:95-97`); no antiforgery (H-02). |
| `GET /api/health` | Anonymous | Discloses configuration validity to unauthenticated callers. |
| `GET /api/config` | Cookie | Returns browser-safe config (region, model, voice, avatar settings, turn-taking mode, agent metadata, safe questions). |
| `GET /ws/session` | Cookie + `Origin` check | Consumes a concurrency slot and starts billing. `Origin` validation rejects requests from outside allowed origins (same-origin or configured `AllowedOrigins`); non-browser clients with no `Origin` header are allowed through. |
| `say` WebSocket frame | Cookie (session already open) | **Arbitrary text spoken on stage. Unconstrained (H-01).** |
| `config/*.json` and `config/grounding/*.md` | Filesystem | Anyone who can change these changes avatar behaviour. **The grounding markdown is not merely data:** in model mode `config/grounding/company-direction.md` becomes `VoiceLiveSessionOptions.Instructions` verbatim, so write access to it is equivalent to write access to what the avatar says. Deployment-time trust, unverified at runtime. |

## Assumptions this design trusts without verifying

**This is the section to re-read whenever the deployment changes.**

| Assumption | Status | If false |
|---|---|---|
| The network is trusted and access is limited to the event team | **Not enforced by anything.** `azd up` yields a public endpoint with no IP restrictions | Every row below becomes exploitable by anyone on the internet |
| An authenticated user is benign | **Accepted risk, deliberately** | Arbitrary avatar speech in front of an audience ([H-01](../review-merged.md#h-01--say-control-frame-is-an-unrestricted-prompt-injection-and-cost-channel--high)) |
| The client IP seen by the rate limiter is real | **False today** — forwarded headers are unvalidated | Per-IP login rate limiting is bypassable ([C-01](../review-merged.md#c-01--login-rate-limiter-bypassable-via-spoofed-x-forwarded-for--critical)) |
| One shared credential is sufficient identity | **Accepted for this deployment shape** | No attribution, no per-person revocation ([ADR 0003](adr/0003-shared-cookie-authentication.md)). Changing the password or username does **not** revoke live sessions; the 8-hour **sliding** cookie renews on each request. The only revocation path is destroying the ASP.NET Data Protection key ring — restarting the app does not suffice on App Service. |
| The operator credential is not in source control | **Partly enforced.** `Development_settings_carry_no_auth_section` genuinely enforces it for `appsettings.Development.json`. For documentation it is only a **regression guard**: `Maintained_markdown_publishes_no_credential_literals` matches the one retired literal, in backtick form, and cannot detect a *new* credential published in docs | Public credential disclosure ([C-02](../review-merged.md#c-02--working-credentials-committed-to-the-repository--critical)) |
| Config files are only writable by deployers | Deployment-time trust, unverified at runtime | Arbitrary behaviour change with no audit |
| Azure output is safe to show an audience | Trusted; no content filtering in this app | Whatever the model produces reaches the stage |
| `Auth__Password` in App Service settings is protected | **Not enforced** — `infra/resources.bicep:89` writes `Auth__Password` as a plaintext app setting on every `azd up`, clobbering any Key Vault reference set between provisions ([M-02](../review-merged.md#m-02--auth__password-stored-as-a-plaintext-app-service-setting--high)) | The credential is visible as a plaintext App Service setting after any re-provision |

## Accepted risks

Stated so they are decisions rather than oversights:

1. **Any authenticated user can make the avatar say anything.** Accepted because the authenticated population is the event team. Unacceptable the moment that population grows — fix [H-01](../review-merged.md#h-01--say-control-frame-is-an-unrestricted-prompt-injection-and-cost-channel--high) first.
2. **No per-operator identity or audit trail.** Accepted for a single-event deployment.
3. **No session timeout.** Accepted because sessions are attended; it is a live cost risk if that stops being true ([M-01](../review-merged.md#m-01--no-idle-or-absolute-session-timeout-capacity-gate-trivially-exhausted--high)).
4. **Session concurrency cap is per-instance.** The `MaxConcurrentSessions = 2` semaphore is in-process; scale-out multiplies the effective cap. Accepted for single-instance deployments — see [ADR 0005](adr/0005-per-instance-session-cap.md).
5. **No content filtering of avatar output.** Accepted because the model and prompt are controlled and the show is rehearsed.

## Out of scope

Azure platform security, the venue's physical security, and the endpoint security of the operator's laptop.
```

- [x] **Step 2: Link it from the README security section**

`README.md` does have a `### Authentication and trust boundaries` section; a pointer was added there as well as at the end of `## Production readiness`. Link added at the end of `## Production readiness` (after the `docs/production-deployment.md` reference), which is where a reader encounters the security context:

```markdown
Actors, assets, entry points and the assumptions this design trusts without verifying are enumerated in [`docs/threat-model.md`](docs/threat-model.md).
```

- [x] **Step 3: Commit**

```bash
git add docs/threat-model.md README.md docs/superpowers/plans/2026-08-05-documentation-alignment.md
git commit -m "docs: add threat model with explicit trust assumptions and accepted risks"
```

---

## Task 20: D-23 — community-health files and the licence rename ✅ DONE

Four standard files are absent, and `licence.md` is not detected as a licence by GitHub's API, `dotnet pack` or SBOM tooling. `SECURITY.md` is the urgent one: this repository has two reviews finding Critical issues and no channel to report a vulnerability.

**Files:**
- Create: `SECURITY.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `CHANGELOG.md`
- Rename: `licence.md` → `LICENSE.md`

**Spec errors corrected:**

1. **SECURITY.md Scope** — spec said *"the app is documented as not internet-facing"* as the out-of-scope basis. The settled wording (README.md, ADR 0003, production-deployment.md) is: *not internet-facing by intent, but `azd up` publishes a public App Service with no IP restrictions or VNet integration; restricting access is the operator's responsibility.* Fixed to match.

2. **SECURITY.md SHA** — `d5110dc` confirmed valid: `review-merged.md:5` states *"Commit reviewed: d5110dc (docs: add MIT License file)"*. SHA retained.

3. **CONTRIBUTING.md Node.js version** — spec said Node.js 20+. CI pins `node-version: 24`. Fixed to 24.

4. **CONTRIBUTING.md test count** — spec said "90 tests". Actual total is 101. Removed hardcoded count; text now reads "no frontend build" without a number.

5. **CONTRIBUTING.md dotnet run command** — the spec omitted `--no-launch-profile`; an intermediate revision wrongly *added* it, generalising Task 14's rule. Task 14's flag is mandatory only for the `web/README.md` invocation that overrides `ASPNETCORE_URLS` (otherwise the launch profile forces 5280 instead of 5210). For CONTRIBUTING's plain `dotnet run` the flag is actively harmful: verified by execution, it drops `ASPNETCORE_ENVIRONMENT=Development`, so the app runs as Production, the user-secrets provider is never registered, and the two `dotnet user-secrets set` commands above it silently have no effect. Shipped without the flag.
6. **`ConfigDir` was missing from both CONTRIBUTING and the README quickstart** — verified by execution. `dotnet run` sets the working directory to the *project* directory, so the default relative `config` resolves to `web/src/VoiceLive.Web/config`, which does not exist: the run emits five `not found at config/...` validation errors, the app starts unhealthy and `/api/health` returns 503. Adding `ConfigDir=$(pwd)/config` from the repo root clears all five. Both documents now carry it plus `VoiceLive__Endpoint` / `VoiceLive__Mode`.

6. **CONTRIBUTING.md RBAC scopes** — spec said "Cognitive Services User and Foundry User on the Foundry resource" without scopes. Task 11 settled: `Cognitive Services User` on the **account**, `Foundry User` on the **project**. Fixed.

7. **CONTRIBUTING.md Documentation is tested** — spec listed a vague summary. Fixed to enumerate all 11 real `[Fact]` methods from `DocumentationTests.cs`.

8. **CONTRIBUTING.md Key Vault trap** — spec said "use Key Vault references in Azure" without the M-02 caveat. Added known-trap note: `Auth__Password` is overwritten on every `azd provision`; re-apply Key Vault reference after every provision. Cross-references `docs/production-deployment.md` §2.

9. **CHANGELOG docs/README.md and docs/history/** — spec included these in Added. Neither exists yet (Task 21). Omitted.

10. **CHANGELOG session-flow status indicators** — spec said "the six status indicators" with no names. Fixed to name all six: `connection`, `webrtc`, `microphone`, `turn`, `speech`, `avatar` (matching `docs/session-flow.md`).

- [x] **Step 1: Rename the licence with history preserved**

```bash
git mv licence.md LICENSE.md
```

- [x] **Step 2: Fix any references to the old filename**

No references to `licence.md` found in `README.md`, `web/README.md` or `docs/`. Only review artifacts (`opus-review.md`, `review-merged.md`) and CHANGELOG body text, which are excluded from maintained markdown or are factually describing the rename. No updates needed.

- [x] **Step 3: Create `SECURITY.md`**

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

Out of scope: Azure platform vulnerabilities (report to Microsoft), findings that require an already-compromised operator machine, and issues that depend on ignoring the documented deployment constraints — the app is **not internet-facing by intent, but `azd up` publishes a public App Service with no IP restrictions or VNet integration**. Nothing enforces that boundary; restricting network access is the operator's responsibility. See [Non-goals](README.md#non-goals) and `docs/adr/0003-shared-cookie-authentication.md`.
```

- [x] **Step 4: Create `CONTRIBUTING.md`**

````markdown
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

export VoiceLive__Endpoint="https://<your-resource>.services.ai.azure.com"
export VoiceLive__Mode=model

# Run from the repository root
ConfigDir=$(pwd)/config dotnet run --project web/src/VoiceLive.Web
```

Then open **http://localhost:5280/** and sign in with the credentials you just set.

Two things about that command are easy to get wrong:

- **`ConfigDir` must be absolute.** `dotnet run` sets the app's working directory to the **project** directory (`web/src/VoiceLive.Web`), not the directory you invoked it from, so the default relative `config` resolves to `web/src/VoiceLive.Web/config`, which does not exist. The app still starts, but `/api/health` reports 503 and no session can begin. Use `$(pwd)/config` from the repository root, or `../../../config`.
- **Do not add `--no-launch-profile` here.** The launch profile supplies both `ASPNETCORE_ENVIRONMENT=Development` and port 5280. Without it the app runs as **Production**, which means the user-secrets provider is never added and the two `dotnet user-secrets` commands above have no effect. The flag is only appropriate when you deliberately override `ASPNETCORE_URLS`, as [`web/README.md`](web/README.md) does.

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
````

- [x] **Step 5: Create `CODE_OF_CONDUCT.md`**

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

- [x] **Step 6: Create `CHANGELOG.md`**

```markdown
# Changelog

All notable changes to this project are documented here. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

### Added
- Production deployment guide covering identity, secrets, capacity, cost, observability, environments, rollback, DR, networking and data handling (`docs/production-deployment.md`).
- Authoritative wire-protocol reference for `/ws/session`, including frame payload shapes (`docs/wire-protocol.md`).
- Session flow document covering the turn lifecycle, the six status channels (`connection`, `webrtc`, `microphone`, `turn`, `speech`, `avatar`) and per-view journeys (`docs/session-flow.md`), which also gives the previously orphaned diagrams a home.
- Six architecture decision records (`docs/adr/`).
- Threat model with explicit trust assumptions and accepted risks (`docs/threat-model.md`).
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

- [x] **Step 7: Verify community-health detection and links**

Verified: `ls SECURITY.md CONTRIBUTING.md CODE_OF_CONDUCT.md CHANGELOG.md LICENSE.md` — all five present.

Full suite: **Failed: 1, Passed: 100, Skipped: 0, Total: 101**. Only broken link is `CONTRIBUTING.md -> docs/README.md` (Task 21 forward reference, expected).

- [x] **Step 8: Commit**

```bash
git add SECURITY.md CONTRIBUTING.md CODE_OF_CONDUCT.md CHANGELOG.md LICENSE.md docs/superpowers/plans/2026-08-05-documentation-alignment.md
git commit -m "docs: add community-health files and rename licence.md to LICENSE.md

Co-authored-by: Copilot <223556219+Copilot@users.noreply.github.com>
Copilot-Session: 74a61d1f-17e7-42cc-8135-7e78c446a579"
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
