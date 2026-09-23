namespace DynaDocs.Tests.Steps;

using System.Diagnostics;
using System.Text.RegularExpressions;

public sealed class CanonicalSkillAssertionTests
{
    internal static readonly string[] ProjectPathGuidance =
    [
        "skills/roles/officers/admiral/SKILL.md",
        "skills/productivity/bro/SKILL.md",
        "skills/roles/officers/chief-of-staff/SKILL.md",
        "skills/productivity/co-thinker/SKILL.md",
        "skills/roles/crew/code-writer/SKILL.md",
        "skills/engineering/diagnosing-bugs/SKILL.md",
        "skills/roles/crew/docs-writer/SKILL.md",
        "skills/engineering/domain-modeling/SKILL.md",
        "skills/engineering/improve-codebase-architecture/SKILL.md",
        "skills/roles/crew/inquisitor/SKILL.md",
        "skills/roles/officers/issue-captain/SKILL.md",
        "skills/roles/crew/project-planner/SKILL.md",
        "skills/roles/crew/research/SKILL.md",
        "skills/roles/crew/reviewer/SKILL.md",
        "skills/roles/crew/scout/SKILL.md",
        "skills/productivity/wayfinder/SKILL.md",
        "skills/roles/crew/reviewer/resources/code.md",
        "skills/roles/crew/reviewer/resources/docs.md",
        "skills/roles/crew/reviewer/resources/project-plan.md",
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
        "dydo/understand/scaffold-and-customization.md",
        "dydo/guides/adding-a-command.md",
        "dydo/guides/customizing-roles.md",
        "dydo/guides/getting-started.md",
        "dydo/guides/orchestration-pitfalls.md",
        "dydo/guides/troubleshooting.md",
        "dydo/project/future-features/routine-admiral.md",
        "dydo/reference/about-dynadocs.md",
        "dydo/reference/configuration.md",
        "dydo/reference/dydo-commands.md"
    ];

    [Fact]
    public void CanonicalSkillTree_SortsEverySkillExactlyOnceByKind()
    {
        var expected = new Dictionary<string, string>
        {
            ["admiral"] = "roles/officers", ["issue-captain"] = "roles/officers", ["chief-of-staff"] = "roles/officers",
            ["project-planner"] = "roles/crew", ["code-writer"] = "roles/crew", ["docs-writer"] = "roles/crew",
            ["reviewer"] = "roles/crew", ["inquisitor"] = "roles/crew", ["research"] = "roles/crew", ["scout"] = "roles/crew",
            ["codebase-design"] = "engineering", ["domain-modeling"] = "engineering", ["diagnosing-bugs"] = "engineering",
            ["prototype"] = "engineering", ["wizard"] = "engineering", ["improve-codebase-architecture"] = "engineering",
            ["co-thinker"] = "productivity", ["grilling"] = "productivity", ["grill-me"] = "productivity",
            ["bro"] = "productivity", ["handoff"] = "productivity", ["teach"] = "productivity",
            ["show-me"] = "productivity", ["walkthrough"] = "productivity", ["writing-for-agents"] = "productivity",
            ["writing-for-humans"] = "productivity", ["self-improvement"] = "productivity", ["wayfinder"] = "productivity",
            ["to-project"] = "productivity"
        };
        Assert.Equal(29, expected.Count);
        var root = Path.Combine(RepositoryRoot(), "skills");
        Assert.Equal(["engineering", "productivity", "roles"], ChildDirectoryNames(root));
        Assert.Equal(["crew", "officers"], ChildDirectoryNames(Path.Combine(root, "roles")));

        var skills = CanonicalSkillSteps.CanonicalSkills(root);
        Assert.Equal(
            expected.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            skills.Select(skill => KeyValuePair.Create(skill.Name, skill.Category)).OrderBy(pair => pair.Key, StringComparer.Ordinal));
        foreach (var skill in skills)
        {
            var body = File.ReadAllText(Path.Combine(skill.Directory, "SKILL.md"));
            Assert.Matches(new Regex($@"(?m)^name: {Regex.Escape(skill.Name)}$"), body);
        }
    }

    [Fact]
    public void CanonicalSkills_WalkCategoriesToAnyDepthButNeverIntoASkill()
    {
        using var tree = new SkillTree();
        tree.Skill("roles/crew/reviewer");
        tree.Skill("engineering/prototype");
        Directory.CreateDirectory(Path.Combine(tree.Root, "roles", "crew", "reviewer", "resources", "nested"));

        var skills = CanonicalSkillSteps.CanonicalSkills(tree.Root);

        Assert.Equal(
            [("prototype", "engineering"), ("reviewer", "roles/crew")],
            skills.Select(skill => (skill.Name, skill.Category)).OrderBy(pair => pair.Name, StringComparer.Ordinal));
    }

    [Fact]
    public void CanonicalSkills_RefuseAnEmptyNestedCategory()
    {
        using var tree = new SkillTree();
        tree.Skill("roles/crew/reviewer");
        Directory.CreateDirectory(Path.Combine(tree.Root, "roles", "officers"));

        var error = Assert.Throws<InvalidOperationException>(() => CanonicalSkillSteps.CanonicalSkills(tree.Root));
        Assert.Contains("officers", error.Message);
    }

    [Fact]
    public void CanonicalSkills_RefuseOneNameReachedThroughTwoCategories()
    {
        using var tree = new SkillTree();
        tree.Skill("engineering/scout");
        tree.Skill("roles/crew/scout");

        var error = Assert.Throws<InvalidOperationException>(() => CanonicalSkillSteps.CanonicalSkills(tree.Root));
        Assert.Contains("Duplicate skill name across categories: scout", error.Message);
    }

    private sealed class SkillTree : IDisposable
    {
        public string Root { get; } = Directory.CreateTempSubdirectory("dyd229-skills-").FullName;

        public void Skill(string relative)
        {
            var directory = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "SKILL.md"), $"---\nname: {Path.GetFileName(directory)}\n---\n");
        }

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private static string[] ChildDirectoryNames(string directory) =>
        Directory.EnumerateDirectories(directory).Select(child => Path.GetFileName(child)!).Order(StringComparer.Ordinal).ToArray();

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
        Assert.Equal(20, ProjectPathGuidance.Length);
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
    public void CodeWriter_InlinesTheWorkingTreeChecksAndTheImplementedFormVerbatim()
    {
        var root = RepositoryRoot();
        var skill = File.ReadAllText(Path.Combine(root, "skills", "roles", "crew", "code-writer", "SKILL.md"));
        var contract = File.ReadAllText(Path.Combine(root, "dydo", "guides", "working-tree-contract.md"));
        var standard = File.ReadAllText(Path.Combine(root, "dydo", "reference", "linear-workspace-standard.md"));

        var checks = NumberedItemsUnder(contract, "## Before the first edit");
        Assert.Equal(5, checks.Length);
        Assert.Equal(checks, NumberedItemsUnder(skill, "## Before the first edit"));
        Assert.Equal(ImplementedForm(standard), ImplementedForm(skill));
    }

    private static string[] NumberedItemsUnder(string body, string heading)
    {
        var section = Regex.Match(body.ReplaceLineEndings("\n"), $@"(?ms)^{Regex.Escape(heading)}\n(.*?)(?=^## |\z)");
        Assert.True(section.Success, $"no {heading} section");
        return Regex.Matches(section.Groups[1].Value, @"(?m)^\s*\d+\.\s+(.+?)\s*$")
            .Select(match => match.Groups[1].Value)
            .ToArray();
    }

    private static string ImplementedForm(string body)
    {
        var form = Regex.Match(body, "`(IMPLEMENTED — [^`]+)`");
        Assert.True(form.Success, "no `IMPLEMENTED — ...` form line");
        return form.Groups[1].Value;
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
    [InlineData("roles")]
    [InlineData("engineering")]
    [InlineData("productivity")]
    public void CategoryReadme_ListsEveryLocalSkillOnceWithItsInvocationMode(string category)
    {
        var root = RepositoryRoot();
        var categoryRoot = Path.Combine(root, "skills", category);
        var actualSkills = CanonicalSkillSteps.CanonicalSkills(Path.Combine(root, "skills"))
            .Where(skill => skill.Category == category || skill.Category.StartsWith($"{category}/", StringComparison.Ordinal))
            .OrderBy(skill => skill.Name, StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(actualSkills);

        var readme = File.ReadAllText(Path.Combine(categoryRoot, "README.md"));
        var listed = Regex.Matches(readme, @"(?m)^- \*\*([a-z0-9-]+)\*\* \((user-invoked|model-invoked)\): (.+)$")
            .Select(match => (Name: match.Groups[1].Value, Mode: match.Groups[2].Value, Description: match.Groups[3].Value))
            .ToArray();

        Assert.Equal(actualSkills.Length, listed.Length);
        Assert.Equal(actualSkills.Select(skill => skill.Name), listed.Select(entry => entry.Name).Order(StringComparer.Ordinal));
        var headings = HeadingOfEachListedSkill(readme);

        foreach (var (name, skillCategory, directory) in actualSkills)
        {
            var matches = listed.Where(entry => entry.Name == name).ToArray();
            Assert.True(matches.Length == 1, $"{category}/README.md must list {name} exactly once");
            if (skillCategory != category)
            {
                var subcategory = skillCategory[(category.Length + 1)..];
                Assert.True(headings.GetValueOrDefault(name) == char.ToUpperInvariant(subcategory[0]) + subcategory[1..],
                    $"{category}/README.md must list {name} under the heading of its {subcategory}/ folder");
            }

            var body = File.ReadAllText(Path.Combine(directory, "SKILL.md"));
            var expectDisabled = Regex.IsMatch(body, @"(?m)^disable-model-invocation:\s*true$");
            var expectedMode = expectDisabled ? "user-invoked" : "model-invoked";
            Assert.Equal(expectedMode, matches[0].Mode);

            var frontmatterDescription = DecodeYamlScalar(Regex.Match(body, @"(?m)^description:\s*(.+)$").Groups[1].Value);
            Assert.False(string.IsNullOrEmpty(frontmatterDescription), $"{name} has no frontmatter description to compare against");
            Assert.Equal(frontmatterDescription, matches[0].Description);
        }
    }

    [Fact]
    public void EveryCanonicalSkill_PairsItsClaudeInvocationFrontmatterWithCodexOpenAiYaml()
    {
        var mismatches = new List<string>();
        foreach (var (name, _, directory) in CanonicalSkillSteps.CanonicalSkills(Path.Combine(RepositoryRoot(), "skills")))
        {
            var body = File.ReadAllText(Path.Combine(directory, "SKILL.md")).ReplaceLineEndings("\n");
            var frontmatter = Regex.Match(body, @"\A---\n(.*?)\n---\n", RegexOptions.Singleline).Groups[1].Value;
            var explicitOnly = Regex.IsMatch(frontmatter, @"(?m)^disable-model-invocation:\s*true\s*$");
            var hint = Regex.Match(frontmatter, @"(?m)^argument-hint:\s*(.+?)\s*$");

            var yamlPath = Path.Combine(directory, "agents", "openai.yaml");
            if (!File.Exists(yamlPath))
            {
                if (explicitOnly || hint.Success)
                    mismatches.Add($"{name}: SKILL.md sets Claude invocation metadata but agents/openai.yaml is missing");
                continue;
            }

            var codex = NestedYamlValues(File.ReadAllText(yamlPath));
            if (explicitOnly != (codex.GetValueOrDefault("policy.allow_implicit_invocation") == "false"))
                mismatches.Add($"{name}: disable-model-invocation: true and policy.allow_implicit_invocation: false must appear together");

            var prompt = codex.GetValueOrDefault("interface.default_prompt");
            if (hint.Success != (prompt is not null)
                || hint.Success && DecodeYamlScalar(hint.Groups[1].Value) != DecodeYamlScalar(prompt!))
                mismatches.Add($"{name}: argument-hint and interface.default_prompt must appear together with the same text");
        }

        Assert.True(mismatches.Count == 0, string.Join("\n", mismatches));
    }

    private static Dictionary<string, string> NestedYamlValues(string yaml)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string? parent = null;
        foreach (var line in yaml.ReplaceLineEndings("\n").Split('\n'))
        {
            var top = Regex.Match(line, @"^([A-Za-z_][\w-]*):\s*$");
            if (top.Success) { parent = top.Groups[1].Value; continue; }
            var child = Regex.Match(line, @"^[ \t]+([A-Za-z_][\w-]*):\s*(.+?)\s*$");
            if (child.Success && parent is not null) values[$"{parent}.{child.Groups[1].Value}"] = child.Groups[2].Value;
            else if (line.Length > 0 && !char.IsWhiteSpace(line[0])) parent = null;
        }
        return values;
    }

    private static Dictionary<string, string> HeadingOfEachListedSkill(string readme)
    {
        var headings = new Dictionary<string, string>(StringComparer.Ordinal);
        string? heading = null;
        foreach (var line in readme.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal)) heading = line[3..].Trim();
            var entry = Regex.Match(line, @"^- \*\*([a-z0-9-]+)\*\*");
            if (entry.Success && heading is not null) headings[entry.Groups[1].Value] = heading;
        }
        return headings;
    }

    private static string DecodeYamlScalar(string raw)
    {
        var trimmed = raw.Trim();
        return trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"'
            ? trimmed[1..^1]
            : trimmed;
    }

    [Fact]
    public void SkillsReadme_CategoryCountsMatchTheRealDirectoriesAndSumTo29()
    {
        var root = RepositoryRoot();
        var skillsRoot = Path.Combine(root, "skills");
        var categories = new[] { "roles", "engineering", "productivity" };
        var actualCounts = CanonicalSkillSteps.CanonicalSkills(skillsRoot)
            .GroupBy(skill => skill.Category.Split('/')[0])
            .ToDictionary(group => group.Key, group => group.Count());
        Assert.Equal(categories.Order(StringComparer.Ordinal), actualCounts.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(29, actualCounts.Values.Sum());

        var readme = File.ReadAllText(Path.Combine(skillsRoot, "README.md"));
        foreach (var category in categories)
        {
            var match = Regex.Match(readme, $@"`{category}/`\]\({category}/README\.md\) — (\d+) skills\.");
            Assert.True(match.Success, $"skills/README.md does not cite a skill count for {category}/");
            Assert.Equal(actualCounts[category], int.Parse(match.Groups[1].Value));
        }

        Assert.Contains(
            $"{actualCounts["roles"]} + {actualCounts["engineering"]} + {actualCounts["productivity"]} = 29",
            readme);
    }

    [Theory]
    [InlineData("see `skills/reviewer/SKILL.md` for the shape", "skills/reviewer")]
    [InlineData("edit `skills/teach/resources/mission-format.md` directly", "skills/teach")]
    [InlineData("authored at `skills/admiral`", "skills/admiral")]
    [InlineData("canonical folder is `skills/orchestration/reviewer/SKILL.md`", "skills/orchestration/reviewer")]
    [InlineData("see `skills/engineering/code-writer/` for the method", "skills/engineering/code-writer")]
    [InlineData("see `skills/roles/scout/`", "skills/roles/scout")]
    public void StaleSkillPaths_ReportAFlatOrFormerCategoryReference(string content, string stale) =>
        Assert.Equal([stale], CanonicalSkillSteps.StaleSkillPaths(RepositoryRoot(), content));

    [Theory]
    [InlineData("canonical folder is `skills/roles/crew/reviewer/SKILL.md`")]
    [InlineData("edit `skills/productivity/teach/resources/mission-format.md` directly")]
    [InlineData("authored at `skills/roles/officers/admiral`")]
    [InlineData("see `skills/roles/officers/` and `skills/engineering/` for the categories")]
    [InlineData("projected at `.claude/skills/reviewer/` and `.agents/skills/reviewer/`")]
    [InlineData("nothing here names a skills/<category>/<name>/ literal")]
    public void StaleSkillPaths_IgnoreACanonicalCategoryOrHostProjectionReference(string content) =>
        Assert.Empty(CanonicalSkillSteps.StaleSkillPaths(RepositoryRoot(), content));

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "DynaDocs.csproj"))) return directory.FullName;
        throw new DirectoryNotFoundException($"Could not locate repository root from {AppContext.BaseDirectory}");
    }
}
