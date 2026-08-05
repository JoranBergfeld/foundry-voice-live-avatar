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

    private static string ConfigSchema() => File.ReadAllText(Path.Combine(RepoRoot, "docs", "config-schema.md"));

    [Fact]
    public void Config_schema_documents_only_voice_types_the_session_builder_supports()
    {
        // Values the app can actually build a session with. `azure-custom` passes WebConfig
        // validation but throws in SessionOptionsBuilder, so it must not be documented as allowed.
        string[] buildable = ["azure-realtime-native", "azure-standard", "openai"];
        string[] notBuildable = ["azure-custom"];

        var schema = ConfigSchema();

        foreach (var value in buildable)
            Assert.True(schema.Contains($"`{value}`", StringComparison.Ordinal),
                $"docs/config-schema.md must document supported voice type '{value}'.");

        foreach (var value in notBuildable)
            Assert.False(schema.Contains($"`{value}`, ", StringComparison.Ordinal) ||
                         schema.Contains($", `{value}`", StringComparison.Ordinal),
                $"docs/config-schema.md lists '{value}' as an allowed voice.type, but SessionOptionsBuilder throws on it. " +
                "Remove it from the allowed-values lists (see review-merged.md H-03 / D-04).");
    }

    [Fact]
    public void Agent_config_ships_no_keys_the_code_never_reads()
    {
        // Keys with zero references anywhere in the codebase. Shipping them implies behaviour
        // that does not exist (review-merged.md H-04 / D-03).
        string[] unimplemented = ["agentVersion", "conversationResumePolicy", "groundingStrategy"];

        var path = Path.Combine(RepoRoot, "config", "agent.json");
        using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));

        var present = unimplemented
            .Where(key => doc.RootElement.TryGetProperty(key, out _))
            .ToList();

        Assert.True(present.Count == 0,
            "config/agent.json ships keys that no code reads: " + string.Join(", ", present) +
            ". Remove them, or implement and validate them.");
    }

    [Fact]
    public void Config_schema_documents_no_unimplemented_agent_keys()
    {
        string[] unimplemented = ["agentVersion", "conversationResumePolicy", "groundingStrategy"];
        var schema = ConfigSchema();

        var documented = unimplemented
            .Where(key => schema.Contains($"`{key}`", StringComparison.Ordinal))
            .ToList();

        Assert.True(documented.Count == 0,
            "docs/config-schema.md documents agent.json keys that no code reads: " + string.Join(", ", documented) +
            ". The schema also promises they 'fail fast at startup', which is false.");
    }
}
