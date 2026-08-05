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

    /// <summary>Credential literals that must never appear as code-formatted values in maintained
    /// documentation. Add to this list whenever a credential is retired, never remove from it.
    /// <para>
    /// IMPORTANT: each entry is the <em>rendered inline-code (backtick-wrapped) form</em> of the
    /// secret, e.g. <c>`rehearsal`</c>. A fenced-block or quoted-JSON appearance of the same
    /// secret would need its own separate entry — a bare word such as <c>rehearsal</c> added here
    /// does NOT cover those appearances.
    /// </para>
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
        // A missing file should fail, not pass vacuously: this test guards against committed
        // credentials, so a missing file means we cannot verify the guard holds. Fail loudly.
        Assert.True(
            File.Exists(path),
            $"appsettings.Development.json not found at '{path}'. The file is expected to exist (without an Auth section).");
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));

        Assert.False(
            doc.RootElement.TryGetProperty("Auth", out _),
            "appsettings.Development.json must not contain an Auth section. Use `dotnet user-secrets` so credentials are never committed.");
    }

    [Fact]
    public void Development_settings_carry_no_voicelive_endpoint()
    {
        // C-02 (review-merged.md): the VoiceLive:Endpoint must come from the operator's environment
        // (export VoiceLive__Endpoint=...) or user-secrets, not from a committed file that would
        // hard-code a real hostname into the repository. A missing file fails loudly for the same
        // reason as Development_settings_carry_no_auth_section.
        var path = Path.Combine(RepoRoot, "web", "src", "VoiceLive.Web", "appsettings.Development.json");
        Assert.True(
            File.Exists(path),
            $"appsettings.Development.json not found at '{path}'. The file must exist with only non-sensitive logging overrides.");
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));

        Assert.False(
            doc.RootElement.TryGetProperty("VoiceLive", out var vl) &&
            vl.TryGetProperty("Endpoint", out var ep) &&
            ep.GetString() is { Length: > 0 },
            "appsettings.Development.json must not contain a VoiceLive:Endpoint value. " +
            "Set it via `export VoiceLive__Endpoint=...` or `dotnet user-secrets set VoiceLive:Endpoint ...`.");
    }

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
}
