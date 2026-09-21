namespace DynaDocs.Tests.Steps;

using System.Collections;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Reqnroll;

[Binding]
[Scope(Feature = "One canonical skill tree reaches every supported host")]
public sealed class CanonicalSkillSteps(CliScenario scenario)
{
    private readonly Dictionary<string, Fingerprint> _ownedFiles = [];
    private readonly Dictionary<string, string?> _projectionTargets = [];
    private byte[]? _ownedDirectoryBody;
    private string? _wrongLinkTarget;
    private CliResult _result = new(-1, "", "");

    [Given("a project containing the canonical {string} skill and the skill setup script")]
    public void ProjectWithCanonicalSkill(string name)
    {
        var category = CanonicalCategory(name);
        CopyDirectory(Path.Combine(RepositoryRoot(), "skills", category, name), Local("skills", category, name));
        Assert.True(Directory.Exists(Local("skills", category, name)));
        File.Copy(Path.Combine(RepositoryRoot(), "setup-skills.mjs"), Local("setup-skills.mjs"));
        File.WriteAllText(Local(".gitignore"), "/.claude/skills/\n/.agents/skills/\n");
    }

    [Given("unrelated Claude and Codex skills and host configuration files with recorded bytes")]
    public void UnrelatedHostFiles()
    {
        RecordFile(".claude/skills/local-only/SKILL.md", "local claude skill\n");
        RecordFile(".claude/settings.local.json", "{\"permissions\":[]}\n");
        RecordFile(".agents/skills/local-only/SKILL.md", "local codex skill\n");
        RecordFile(".agents/config.json", "{\"local\":true}\n");
        Run("git", "init").AssertSuccess();
        Run("git", "config", "user.email", "dyd91@example.invalid").AssertSuccess();
        Run("git", "config", "user.name", "DYD-91 canary").AssertSuccess();
        Run("git", "add", ".").AssertSuccess();
        Run("git", "add", "-f", ".claude", ".agents").AssertSuccess();
        Run("git", "commit", "-m", "fixture").AssertSuccess();
    }

    [When("I set up the canonical skills")]
    public void Setup() => _result = Run("node", "setup-skills.mjs");

    [When("I set up the canonical skills again")]
    public void SetupAgain()
    {
        RecordProjection(".claude/skills/teach");
        RecordProjection(".agents/skills/teach");
        Setup();
    }

    [Then("setup succeeds")]
    public void SetupSucceeds() => _result.AssertSuccess();

    [Then("the Claude and Codex {string} entries resolve to the canonical skill directory")]
    public void HostEntriesResolve(string name)
    {
        var canonical = RealPath(Local("skills", CanonicalCategory(name), name));
        Assert.Equal(canonical, RealPath(Local(".claude", "skills", name)));
        Assert.Equal(canonical, RealPath(Local(".agents", "skills", name)));
        Assert.True(new DirectoryInfo(Local(".claude", "skills", name)).LinkTarget is not null);
        Assert.True(new DirectoryInfo(Local(".agents", "skills", name)).LinkTarget is not null);
    }

    [Then("no OpenCode {string} projection is created")]
    public void NoOpenCodeProjection(string name) => Assert.False(Directory.Exists(Local(".opencode", "skills", name)));

    [Then("every unrelated skill and host configuration file keeps its recorded bytes")]
    public void OwnedFilesUnchanged()
    {
        Assert.Equal(4, _ownedFiles.Count);
        foreach (var (relative, expected) in _ownedFiles)
            Assert.Equal(expected, Fingerprint.File(Local(relative)));
    }

    [Then("the host projections are unchanged")]
    public void ProjectionsUnchanged()
    {
        Assert.Equal(2, _projectionTargets.Count);
        foreach (var (relative, target) in _projectionTargets)
            Assert.Equal(target, new DirectoryInfo(Local(relative)).LinkTarget);
    }

    [Then("the Git working tree is clean")]
    public void GitClean()
    {
        var result = Run("git", "status", "--short");
        result.AssertSuccess();
        Assert.Equal("", result.Stdout);
        RemoveHostSkillLinks();
        ClearReadOnly(Local(".git"));
        Directory.Delete(Local(".git"), recursive: true);
    }

    [Then("the canonical {string} skill keeps its Claude invocation frontmatter")]
    public void ClaudeMetadata(string name)
    {
        var body = File.ReadAllText(Local("skills", CanonicalCategory(name), name, "SKILL.md"));
        Assert.StartsWith("---\n", body.ReplaceLineEndings("\n"));
        Assert.Contains("argument-hint: \"What would you like to learn about?\"", body);
        Assert.Contains("disable-model-invocation: true", body);
    }

    [Then("the canonical {string} skill keeps its Codex invocation metadata")]
    public void CodexMetadata(string name)
    {
        var metadata = File.ReadAllText(Local("skills", CanonicalCategory(name), name, "agents", "openai.yaml"));
        Assert.Contains("allow_implicit_invocation: false", metadata);
        Assert.Contains("default_prompt: \"What would you like to learn about?\"", metadata);
    }

    [Then("every resource linked by the canonical {string} body resolves inside its skill directory")]
    public void ResourceLinksResolve(string name)
    {
        var skill = Local("skills", CanonicalCategory(name), name);
        var links = MarkdownLinks(File.ReadAllText(Path.Combine(skill, "SKILL.md")))
            .Where(link => link.StartsWith("resources/", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(links);
        foreach (var link in links)
        {
            var target = Path.GetFullPath(Path.Combine(skill, link.Replace('/', Path.DirectorySeparatorChar)));
            Assert.StartsWith(Path.GetFullPath(skill) + Path.DirectorySeparatorChar, target);
            Assert.True(File.Exists(target), $"Missing resource link: {link}");
        }
    }

    [Then("every skill link resolves when followed from each location")]
    public void EveryLinkResolves(Table locations)
    {
        CopyRemainingSkills();
        CopyProjectPathTargets();
        Setup();
        SetupSucceeds();
        try
        {
            Assert.Equal(3, locations.Rows.Count);
            var canonicalRoot = Local("skills");
            foreach (var categoryRoot in Directory.EnumerateDirectories(canonicalRoot))
            foreach (var canonicalSkill in Directory.EnumerateDirectories(categoryRoot))
            {
                var category = Path.GetFileName(categoryRoot);
                var name = Path.GetFileName(canonicalSkill);
                var markdownFiles = Directory.EnumerateFiles(canonicalSkill, "*.md", SearchOption.AllDirectories).ToArray();
                Assert.NotEmpty(markdownFiles);
                foreach (var canonicalFile in markdownFiles)
                {
                    var relativeFile = Path.GetRelativePath(canonicalSkill, canonicalFile);
                    var canonicalBytes = Fingerprint.File(canonicalFile);
                    var links = MarkdownLinks(File.ReadAllText(canonicalFile)).Where(IsRepositoryRelativeFileLink).ToArray();
                    foreach (var link in links)
                    {
                        var canonicalTarget = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(canonicalFile)!, FileLinkPath(link)));
                        AssertInside(canonicalSkill, canonicalTarget, $"{Path.GetRelativePath(Local(), canonicalFile)}: {link}");
                        Assert.True(File.Exists(canonicalTarget), $"Missing skill-local link: {canonicalFile}: {link}");
                    }

                    foreach (var row in locations.Rows)
                    {
                        var location = row["location"]
                            .Replace("<category>", category, StringComparison.Ordinal)
                            .Replace("<name>", name, StringComparison.Ordinal);
                        var viewRoot = Local(location.Replace('/', Path.DirectorySeparatorChar));
                        var viewFile = Path.Combine(viewRoot, relativeFile);
                        Assert.Equal(canonicalBytes, Fingerprint.File(viewFile));
                        Assert.Equal(Path.GetFullPath(canonicalSkill), RealPath(viewRoot));
                        foreach (var link in links)
                        {
                            var lexicalTarget = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(viewFile)!, FileLinkPath(link)));
                            Assert.True(File.Exists(lexicalTarget), $"{location}/{relativeFile}: {link}");
                            var projectedCanonicalTarget = Path.GetFullPath(Path.Combine(RealPath(viewRoot), Path.GetDirectoryName(relativeFile) ?? "", FileLinkPath(link)));
                            var expectedCanonicalTarget = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(canonicalFile)!, FileLinkPath(link)));
                            Assert.Equal(expectedCanonicalTarget, projectedCanonicalTarget);
                            Assert.Equal(Fingerprint.File(expectedCanonicalTarget), Fingerprint.File(lexicalTarget));
                        }
                    }
                }
            }

            Assert.False(FileSystemInfoExists(Local(".claude", "dydo")));
            Assert.False(FileSystemInfoExists(Local(".agents", "dydo")));
            AssertProjectPathsUseRepositoryRoot(locations);
        }
        finally
        {
            RemoveHostSkillLinks();
        }
    }

    [Then(@"^current documentation and template mirrors describe skills/<category>/<name> as the only editable source$")]
    public void CurrentGuidanceUsesCanonicalSource()
    {
        foreach (var relative in CanonicalSkillAssertionTests.CurrentGuidance)
        {
            var content = File.ReadAllText(Path.Combine(RepositoryRoot(), relative));
            Assert.Contains("skills/<category>/", content);
            Assert.DoesNotContain("edit both", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("edit each host", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("committed copy", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Then("canonical agent guidance does not instruct agents to maintain or compare per-host skill copies")]
    public void AgentGuidanceUsesCanonicalSource()
    {
        foreach (var relative in new[]
        {
            "skills/orchestration/docs-writer/SKILL.md",
            "skills/productivity/writing-for-agents/resources/skill-mechanics.md"
        })
        {
            var content = File.ReadAllText(Path.Combine(RepositoryRoot(), relative));
            Assert.DoesNotContain("both copies", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("edit both", content, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("each host's copy", content, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Given("a human-owned Claude {string} directory")]
    public void HumanOwnedClaudeDirectory(string name) => CreateOwnedDirectory(".claude", name);

    [Given("no Claude {string} entry exists")]
    public void NoClaudeEntry(string name) => Assert.False(FileSystemInfoExists(Local(".claude", "skills", name)));

    [Given("a human-owned Codex {string} directory exists")]
    public void HumanOwnedCodexDirectory(string name) => CreateOwnedDirectory(".agents", name);

    [Then("setup fails without changing the human-owned directory")]
    public void SetupFailsAndOwnedDirectoryUnchanged()
    {
        Assert.NotEqual(0, _result.ExitCode);
        Assert.NotNull(_ownedDirectoryBody);
        var candidates = new[] { Local(".claude", "skills", "teach", "SKILL.md"), Local(".agents", "skills", "teach", "SKILL.md") };
        var existing = candidates.Single(File.Exists);
        Assert.Equal(_ownedDirectoryBody, File.ReadAllBytes(existing));
    }

    [Then("no Codex {string} projection is created")]
    public void NoCodexProjection(string name) => Assert.False(FileSystemInfoExists(Local(".agents", "skills", name)));

    [Then("no Claude {string} projection is created")]
    public void NoClaudeProjection(string name) => Assert.False(FileSystemInfoExists(Local(".claude", "skills", name)));

    [Given("the Claude {string} entry links to a different directory")]
    public void ClaudeEntryLinksElsewhere(string name)
    {
        _wrongLinkTarget = Local("elsewhere", name);
        Directory.CreateDirectory(_wrongLinkTarget);
        Directory.CreateDirectory(Local(".claude", "skills"));
        CreateDirectoryLink(Local(".claude", "skills", name), _wrongLinkTarget);
    }

    [Then("setup fails without replacing the existing link")]
    public void WrongLinkUnchanged()
    {
        Assert.NotEqual(0, _result.ExitCode);
        Assert.NotNull(_wrongLinkTarget);
        Assert.Equal(RealPath(_wrongLinkTarget), RealPath(Local(".claude", "skills", "teach")));
        RemoveLink(".claude/skills/teach");
    }

    private void CreateOwnedDirectory(string host, string name)
    {
        var body = Local(host, "skills", name, "SKILL.md");
        Directory.CreateDirectory(Path.GetDirectoryName(body)!);
        _ownedDirectoryBody = Encoding.UTF8.GetBytes("human-owned\n");
        File.WriteAllBytes(body, _ownedDirectoryBody);
    }

    private void RecordFile(string relative, string content)
    {
        var full = Local(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        _ownedFiles[relative] = Fingerprint.File(full);
    }

    private void CopyProjectPathTargets()
    {
        var repository = RepositoryRoot();
        var targets = CanonicalSkillAssertionTests.ProjectPathGuidance
            .SelectMany(relative => ProjectPathLiterals(File.ReadAllText(Path.Combine(repository, relative))))
            .Distinct(StringComparer.Ordinal);
        foreach (var relative in targets)
        {
            var source = Path.Combine(repository, FileLinkPath(relative));
            var target = Local(FileLinkPath(relative));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target);
        }
    }

    private void CopyRemainingSkills()
    {
        var root = Path.Combine(RepositoryRoot(), "skills");
        foreach (var categorySource in Directory.EnumerateDirectories(root))
        {
            var category = Path.GetFileName(categorySource);
            foreach (var source in Directory.EnumerateDirectories(categorySource))
            {
                var target = Local("skills", category, Path.GetFileName(source));
                if (!Directory.Exists(target)) CopyDirectory(source, target);
            }
        }
    }

    private static string CanonicalCategory(string name)
    {
        var skillsRoot = Path.Combine(RepositoryRoot(), "skills");
        foreach (var category in Directory.EnumerateDirectories(skillsRoot))
            if (Directory.Exists(Path.Combine(category, name))) return Path.GetFileName(category)!;
        throw new DirectoryNotFoundException($"Canonical skill not found in any category: {name}");
    }

    private void RecordProjection(string relative) => _projectionTargets[relative] = new DirectoryInfo(Local(relative)).LinkTarget;
    private void RemoveLink(string relative)
    {
        var full = Local(relative.Replace('/', Path.DirectorySeparatorChar));
        if (new DirectoryInfo(full).LinkTarget is not null) Directory.Delete(full);
    }

    private void RemoveHostSkillLinks()
    {
        foreach (var host in new[] { ".claude", ".agents" })
        {
            var root = Local(host, "skills");
            if (!Directory.Exists(root)) continue;
            foreach (var directory in Directory.EnumerateDirectories(root))
                if (new DirectoryInfo(directory).LinkTarget is not null) Directory.Delete(directory);
        }
    }

    private void AssertProjectPathsUseRepositoryRoot(Table locations)
    {
        var projectRoot = Path.GetFullPath(scenario.DirectoryPath);
        foreach (var relative in CanonicalSkillAssertionTests.ProjectPathGuidance)
        {
            var parts = relative.Split('/');
            var category = parts[1];
            var name = parts[2];
            var relativeFile = Path.Combine(parts.Skip(3).ToArray());
            var canonicalFile = Local(relative.Replace('/', Path.DirectorySeparatorChar));
            var canonicalBody = File.ReadAllText(canonicalFile);
            var projectPaths = ProjectPathLiterals(canonicalBody).ToArray();
            Assert.NotEmpty(projectPaths);
            foreach (var row in locations.Rows)
            {
                var location = row["location"]
                    .Replace("<category>", category, StringComparison.Ordinal)
                    .Replace("<name>", name, StringComparison.Ordinal);
                var viewFile = Path.Combine(Local(location.Replace('/', Path.DirectorySeparatorChar)), relativeFile);
                Assert.Equal(Fingerprint.File(canonicalFile), Fingerprint.File(viewFile));
                var viewBody = File.ReadAllText(viewFile);
                Assert.Equal(projectPaths, ProjectPathLiterals(viewBody).ToArray());
                foreach (var projectPath in projectPaths)
                {
                    var target = Path.GetFullPath(Path.Combine(projectRoot, FileLinkPath(projectPath)));
                    AssertInside(projectRoot, target, $"{location}/{relativeFile}: {projectPath}");
                    Assert.True(File.Exists(target), $"Missing repository-root project path: {projectPath}");
                    Assert.NotEmpty(File.ReadAllBytes(target));
                }
            }
        }
    }

    private static void ClearReadOnly(string directory)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories))
            File.SetAttributes(path, File.GetAttributes(path) & ~FileAttributes.ReadOnly);
    }

    private CliResult Run(string executable, params string[] arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            WorkingDirectory = scenario.DirectoryPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CliResult(process.ExitCode, stdout, stderr);
    }

    private void CreateDirectoryLink(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }
        var result = Run("cmd.exe", "/d", "/c", "mklink", "/J", link, target);
        result.AssertSuccess();
    }

    private string Local(params string[] parts) => Path.Combine([scenario.DirectoryPath, .. parts]);
    private static bool FileSystemInfoExists(string path)
    {
        if (File.Exists(path) || Directory.Exists(path)) return true;
        try { return new DirectoryInfo(path).LinkTarget is not null; }
        catch (IOException) { return false; }
    }
    private static string RealPath(string path) => new DirectoryInfo(path).ResolveLinkTarget(true)?.FullName ?? Path.GetFullPath(path);
    private static bool IsRepositoryRelativeFileLink(string link) => link != "Linear URL" && !link.StartsWith('#') && !Uri.TryCreate(link, UriKind.Absolute, out _);
    private static string FileLinkPath(string link) => link.Replace('/', Path.DirectorySeparatorChar);
    private static IEnumerable<string> MarkdownLinks(string markdown) => Regex.Matches(markdown, @"\[[^\]]+\]\(([^)#]+)(?:#[^)]+)?\)")
        .Select(match => match.Groups[1].Value);
    private static IEnumerable<string> ProjectPathLiterals(string markdown) => Regex.Matches(markdown, @"`(dydo/[^`]+\.md)`")
        .Select(match => match.Groups[1].Value)
        .Where(path => !path.Contains('<') && !path.Contains('>'))
        .Distinct(StringComparer.Ordinal);

    private static void AssertInside(string root, string target, string description)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), Path.GetFullPath(target));
        Assert.False(relative == ".." || relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) || Path.IsPathRooted(relative),
            $"Path escaped {root}: {description}");
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source)) File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(target, Path.GetFileName(directory)));
    }

    private static string RepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "DynaDocs.csproj"))) return directory.FullName;
        throw new DirectoryNotFoundException($"Could not locate repository root from {AppContext.BaseDirectory}");
    }

    private readonly record struct Fingerprint(long Length, string Sha256)
    {
        internal static Fingerprint File(string path)
        {
            var bytes = System.IO.File.ReadAllBytes(path);
            return new(bytes.LongLength, Convert.ToHexString(SHA256.HashData(bytes)));
        }
    }
}
