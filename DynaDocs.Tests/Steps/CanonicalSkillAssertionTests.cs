namespace DynaDocs.Tests.Steps;

using System.Diagnostics;
using System.Text.RegularExpressions;

public sealed class CanonicalSkillAssertionTests
{
    internal static readonly string[] ProjectPathGuidance =
    [
        "skills/orchestration/admiral/SKILL.md",
        "skills/productivity/bro/SKILL.md",
        "skills/orchestration/chief-of-staff/SKILL.md",
        "skills/productivity/co-thinker/SKILL.md",
        "skills/engineering/diagnosing-bugs/SKILL.md",
        "skills/orchestration/docs-writer/SKILL.md",
        "skills/engineering/domain-modeling/SKILL.md",
        "skills/engineering/hardener/SKILL.md",
        "skills/engineering/implementer/SKILL.md",
        "skills/engineering/improve-codebase-architecture/SKILL.md",
        "skills/orchestration/inquisitor/SKILL.md",
        "skills/orchestration/issue-captain/SKILL.md",
        "skills/orchestration/project-planner/SKILL.md",
        "skills/engineering/research/SKILL.md",
        "skills/orchestration/reviewer/SKILL.md",
        "skills/engineering/scout/SKILL.md",
        "skills/engineering/specifier/SKILL.md",
        "skills/orchestration/wayfinder/SKILL.md",
        "skills/orchestration/reviewer/resources/code.md",
        "skills/orchestration/reviewer/resources/docs.md",
        "skills/orchestration/reviewer/resources/project-plan.md",
        "skills/productivity/writing-for-agents/resources/skill-mechanics.md"
    ];

    internal static readonly string[] CurrentGuidance =
    [
        "README.md",
        "THIRD-PARTY-NOTICES.md",
        "npm/README.md",
        "npm/THIRD-PARTY-NOTICES.md",
        "Scaffold/dydo/reference/about-dynadocs.md",
        "Scaffold/dydo/reference/dydo-commands.md",
        "dydo/understand/about.md",
        "dydo/understand/architecture.md",
        "dydo/understand/templates-and-customization.md",
        "dydo/guides/adding-a-command.md",
        "dydo/guides/customizing-roles.md",
        "dydo/guides/getting-started.md",
        "dydo/guides/migrating-dydo-2x-to-3x.md",
        "dydo/guides/orchestration-pitfalls.md",
        "dydo/guides/troubleshooting.md",
        "dydo/project/future-features/routine-admiral.md",
        "dydo/reference/about-dynadocs.md",
        "dydo/reference/configuration.md",
        "dydo/reference/dydo-commands.md"
    ];

    [Fact]
    public void CanonicalSkillTree_HasEveryRoleExactlyOnce()
    {
        string[] expected =
        [
            "admiral", "bro", "chief-of-staff", "co-thinker", "codebase-design", "diagnosing-bugs",
            "docs-writer", "domain-modeling", "grill-me", "grilling", "handoff", "hardener", "implementer",
            "improve-codebase-architecture", "inquisitor", "issue-captain", "project-planner", "prototype",
            "research", "reviewer", "scout", "self-improvement", "show-me", "specifier", "teach", "to-project",
            "walkthrough", "wayfinder", "wizard", "writing-for-agents", "writing-for-humans"
        ];
        string[] categories = ["orchestration", "engineering", "productivity"];
        var root = Path.Combine(RepositoryRoot(), "skills");
        var actualCategories = Directory.EnumerateDirectories(root).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(categories.Order(StringComparer.Ordinal), actualCategories);

        var actual = categories
            .SelectMany(category => Directory.EnumerateDirectories(Path.Combine(root, category)).Select(Path.GetFileName))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual);
        foreach (var category in categories)
        foreach (var skillDirectory in Directory.EnumerateDirectories(Path.Combine(root, category)))
        {
            var name = Path.GetFileName(skillDirectory);
            var body = File.ReadAllText(Path.Combine(skillDirectory, "SKILL.md"));
            Assert.Matches(new Regex($@"(?m)^name: {Regex.Escape(name)}$"), body);
        }
    }

    [Fact]
    public void CanonicalSkillTree_HasNoAuthoredHostCopies()
    {
        var repositoryRoot = RepositoryRoot();
        var canonicalSkills = Path.Combine(repositoryRoot, "skills");
        var trackedFiles = TrackedFilesBelow(repositoryRoot, ".claude/skills", ".agents/skills");

        foreach (var relative in new[] { ".claude/skills", ".agents/skills" })
        {
            var root = Path.Combine(repositoryRoot, relative);
            if (!Directory.Exists(root)) continue;

            foreach (var entry in Directory.EnumerateFileSystemEntries(root))
            {
                var target = new DirectoryInfo(entry).ResolveLinkTarget(returnFinalTarget: true)?.FullName;
                Assert.True(target is not null,
                    $"{Path.GetRelativePath(repositoryRoot, entry)} is a real file or directory, not a link into skills/. " +
                    "DR 049 forbids a second authored copy of a canonical skill outside the skills/ tree.");

                var resolved = Path.GetFullPath(target!);
                var insidePrefix = canonicalSkills + Path.DirectorySeparatorChar;
                Assert.True(resolved.StartsWith(insidePrefix, StringComparison.OrdinalIgnoreCase),
                    $"{Path.GetRelativePath(repositoryRoot, entry)} resolves to {resolved}, which is outside {canonicalSkills}.");
            }
        }

        Assert.Empty(trackedFiles);
    }

    private static string[] TrackedFilesBelow(string repositoryRoot, params string[] relativePaths)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("ls-files");
        start.ArgumentList.Add("--");
        foreach (var relativePath in relativePaths) start.ArgumentList.Add(relativePath);

        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"git ls-files exited {process.ExitCode} in {repositoryRoot}: {stderr}");
        return stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    [Fact]
    public void ProjectKnowledgePaths_AreRepositoryRootLiteralsNotSkillRelativeLinks()
    {
        var root = RepositoryRoot();
        Assert.Equal(22, ProjectPathGuidance.Length);
        foreach (var relative in ProjectPathGuidance)
        {
            var body = File.ReadAllText(Path.Combine(root, relative));
            Assert.DoesNotMatch(@"\]\((?:\.\./)+dydo/", body);
            var projectPaths = Regex.Matches(body, @"`(dydo/[^`]+\.md)`")
                .Select(match => match.Groups[1].Value)
                .Where(path => !path.Contains('<') && !path.Contains('>'))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Assert.NotEmpty(projectPaths);
            foreach (var projectPath in projectPaths)
            {
                var target = Path.GetFullPath(Path.Combine(root, projectPath.Replace('/', Path.DirectorySeparatorChar)));
                Assert.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, target);
                Assert.True(File.Exists(target), $"{relative}: missing repository-root path {projectPath}");
            }
        }
    }

    [Fact]
    public void ThirdPartyNotices_AdaptedInPathsExistOnDisk()
    {
        var root = RepositoryRoot();
        foreach (var relative in new[] { "THIRD-PARTY-NOTICES.md", "npm/THIRD-PARTY-NOTICES.md" })
        {
            var body = File.ReadAllText(Path.Combine(root, relative));
            var referencedPaths = Regex.Matches(body, "`([^`]+)`")
                .Select(match => match.Groups[1].Value)
                .Where(path => path.Contains('/') && !path.Contains('<') && !path.Contains('>')
                    && Regex.IsMatch(path, @"\.[A-Za-z0-9]+$"))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            Assert.NotEmpty(referencedPaths);
            foreach (var referencedPath in referencedPaths)
            {
                var target = Path.Combine(root, referencedPath.Replace('/', Path.DirectorySeparatorChar));
                Assert.True(File.Exists(target), $"{relative}: missing referenced path {referencedPath}");
            }
        }
    }

    [Theory]
    [InlineData("orchestration")]
    [InlineData("engineering")]
    [InlineData("productivity")]
    public void CategoryReadme_ListsEveryLocalSkillOnceWithItsInvocationMode(string category)
    {
        var root = RepositoryRoot();
        var categoryRoot = Path.Combine(root, "skills", category);
        var actualSkills = Directory.EnumerateDirectories(categoryRoot)
            .Select(Path.GetFileName)
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(actualSkills);

        var readme = File.ReadAllText(Path.Combine(categoryRoot, "README.md"));
        var listed = Regex.Matches(readme, @"(?m)^- \*\*([a-z0-9-]+)\*\* \((user-invoked|model-invoked)\):")
            .Select(match => (Name: match.Groups[1].Value, Mode: match.Groups[2].Value))
            .ToArray();

        Assert.Equal(actualSkills.Length, listed.Length);
        Assert.Equal(actualSkills.Order(StringComparer.Ordinal), listed.Select(entry => entry.Name).Order(StringComparer.Ordinal));

        foreach (var name in actualSkills)
        {
            var matches = listed.Where(entry => entry.Name == name).ToArray();
            Assert.True(matches.Length == 1, $"{category}/README.md must list {name} exactly once");

            var body = File.ReadAllText(Path.Combine(categoryRoot, name!, "SKILL.md"));
            var expectDisabled = Regex.IsMatch(body, @"(?m)^disable-model-invocation:\s*true$");
            var expectedMode = expectDisabled ? "user-invoked" : "model-invoked";
            Assert.Equal(expectedMode, matches[0].Mode);
        }
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "DynaDocs.csproj"))) return directory.FullName;
        throw new DirectoryNotFoundException($"Could not locate repository root from {AppContext.BaseDirectory}");
    }
}
