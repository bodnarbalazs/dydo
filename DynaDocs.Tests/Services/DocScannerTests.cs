namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;
using Xunit;

public class DocScannerTests : IDisposable
{
    private readonly string _tempDir;

    public DocScannerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dydo-scanner-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
        GC.SuppressFinalize(this);
    }

    private void WriteDoc(string relativePath, string content = "---\narea: general\ntype: guide\n---\n# Test\n")
    {
        var fullPath = Path.Combine(_tempDir, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private DocScanner NewScanner(IConfigService? configService = null)
    {
        var parser = new MarkdownParser();
        return new DocScanner(parser, configService ?? new StubConfigService(null));
    }

    [Fact]
    public void ScanDirectory_ExcludesLocalWorktreePaths()
    {
        WriteDoc("understand/about.md");
        WriteDoc("_system/.local/worktrees/foo/bar.md");

        var docs = NewScanner().ScanDirectory(_tempDir);

        Assert.Contains(docs, d => d.RelativePath.EndsWith("about.md"));
        Assert.DoesNotContain(docs, d => PathUtils.NormalizePath(d.RelativePath).Contains("_system/.local/"));
    }

    [Fact]
    public void ScanDirectory_ExcludesAuditPaths()
    {
        WriteDoc("understand/about.md");
        WriteDoc("_system/audit/2026/anything.md");

        var docs = NewScanner().ScanDirectory(_tempDir);

        Assert.Contains(docs, d => d.RelativePath.EndsWith("about.md"));
        Assert.DoesNotContain(docs, d => PathUtils.NormalizePath(d.RelativePath).Contains("_system/audit/"));
    }

    [Fact]
    public void ScanDirectory_DoesNotExcludeTemplates()
    {
        WriteDoc("_system/templates/foo.template.md");
        WriteDoc("_system/template-additions/extra-x.md");

        var docs = NewScanner().ScanDirectory(_tempDir);

        Assert.Contains(docs, d => PathUtils.NormalizePath(d.RelativePath).EndsWith("foo.template.md"));
        Assert.Contains(docs, d => PathUtils.NormalizePath(d.RelativePath).EndsWith("extra-x.md"));
    }

    [Fact]
    public void ScanDirectory_HonorsUserAddedScanExclude()
    {
        WriteDoc("understand/about.md");
        WriteDoc("node_modules/some-pkg/README.md");

        var config = new DydoConfig();
        config.ScanExclude.Add("node_modules/");

        var docs = NewScanner(new StubConfigService(config)).ScanDirectory(_tempDir);

        Assert.Contains(docs, d => d.RelativePath.EndsWith("about.md"));
        Assert.DoesNotContain(docs, d => PathUtils.NormalizePath(d.RelativePath).Contains("node_modules/"));
    }

    [Fact]
    public void ScanDirectory_AppliesInternalExcludesEvenWhenConfigMissingThem()
    {
        WriteDoc("understand/about.md");
        WriteDoc("_system/.local/worktrees/foo/bar.md");

        // User scrubbed scanExclude; the scanner still applies the invariants.
        var config = new DydoConfig { ScanExclude = new List<string>() };

        var docs = NewScanner(new StubConfigService(config)).ScanDirectory(_tempDir);

        Assert.DoesNotContain(docs, d => PathUtils.NormalizePath(d.RelativePath).Contains("_system/.local/"));
    }

    private sealed class StubConfigService : IConfigService
    {
        private readonly DydoConfig? _config;

        public StubConfigService(DydoConfig? config) => _config = config;

        public DydoConfig? LoadConfig(string? startPath = null) => _config;

        public string? FindConfigFile(string? startPath = null) => null;
        public void SaveConfig(DydoConfig config, string path) { }
        public string? GetHumanFromEnv() => null;
        public string? GetProjectRoot(string? startPath = null) => null;
        public string GetDydoRoot(string? startPath = null) => "";
        public string GetAgentsPath(string? startPath = null) => "";
        public string GetDocsPath(string? startPath = null) => "";
        public string GetTasksPath(string? startPath = null) => "";
        public string GetAuditPath(string? startPath = null) => "";
        public string GetChangelogPath(string? startPath = null) => "";
        public string GetIssuesPath(string? startPath = null) => "";
        public (bool CanClaim, string? Error) ValidateAgentClaim(string agentName, string? humanName, DydoConfig? config)
            => (true, null);
    }
}
