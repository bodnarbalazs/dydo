namespace DynaDocs.Tests.Steps;

using System.Text.RegularExpressions;

public sealed class CanonicalSkillAssertionTests
{
    internal static readonly string[] ProjectPathGuidance =
    [
        "skills/admiral/SKILL.md",
        "skills/bro/SKILL.md",
        "skills/chief-of-staff/SKILL.md",
        "skills/co-thinker/SKILL.md",
        "skills/diagnosing-bugs/SKILL.md",
        "skills/docs-writer/SKILL.md",
        "skills/domain-modeling/SKILL.md",
        "skills/hardener/SKILL.md",
        "skills/implementer/SKILL.md",
        "skills/improve-codebase-architecture/SKILL.md",
        "skills/inquisitor/SKILL.md",
        "skills/issue-captain/SKILL.md",
        "skills/project-planner/SKILL.md",
        "skills/research/SKILL.md",
        "skills/reviewer/SKILL.md",
        "skills/scout/SKILL.md",
        "skills/specifier/SKILL.md",
        "skills/wayfinder/SKILL.md",
        "skills/reviewer/resources/code.md",
        "skills/reviewer/resources/docs.md",
        "skills/reviewer/resources/project-plan.md",
        "skills/writing-for-agents/resources/skill-mechanics.md"
    ];

    internal static readonly string[] CurrentGuidance =
    [
        "README.md",
        "THIRD-PARTY-NOTICES.md",
        "npm/README.md",
        "npm/THIRD-PARTY-NOTICES.md",
        "Templates/about-dynadocs.template.md",
        "Templates/dydo-commands.template.md",
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
        var root = Path.Combine(RepositoryRoot(), "skills");
        var actual = Directory.EnumerateDirectories(root).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(expected.Order(StringComparer.Ordinal), actual);
        foreach (var name in actual)
        {
            var body = File.ReadAllText(Path.Combine(root, name!, "SKILL.md"));
            Assert.Matches(new Regex($@"(?m)^name: {Regex.Escape(name!)}$"), body);
        }
    }

    [Fact]
    public void CanonicalSkillTree_HasNoAuthoredHostCopies()
    {
        foreach (var relative in new[] { ".claude/skills", ".agents/skills" })
        {
            var root = Path.Combine(RepositoryRoot(), relative);
            if (Directory.Exists(root)) Assert.Empty(Directory.EnumerateFileSystemEntries(root));
        }
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

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "DynaDocs.csproj"))) return directory.FullName;
        throw new DirectoryNotFoundException($"Could not locate repository root from {AppContext.BaseDirectory}");
    }
}
