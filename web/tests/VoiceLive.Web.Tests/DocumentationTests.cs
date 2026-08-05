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
            .Where(rel => !Path.GetFileName(rel).Contains("review", StringComparison.OrdinalIgnoreCase))
            .OrderBy(rel => rel, StringComparer.Ordinal)
            .ToList();
    }

    private static readonly Regex MarkdownLink = new(@"\]\(([^)]+)\)", RegexOptions.Compiled);

    [Fact]
    public void Maintained_markdown_has_no_broken_relative_links()
    {
        var root = RepoRoot;
        var broken = new List<string>();

        foreach (var rel in MaintainedMarkdown())
        {
            var dir = Path.GetDirectoryName(Path.Combine(root, rel))!;
            foreach (Match match in MarkdownLink.Matches(File.ReadAllText(Path.Combine(root, rel))))
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
