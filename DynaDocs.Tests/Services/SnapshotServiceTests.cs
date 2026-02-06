namespace DynaDocs.Tests.Services;

using System.Diagnostics;
using DynaDocs.Services;

public class SnapshotServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _dydoDir;

    public SnapshotServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-snapshot-test-" + Guid.NewGuid().ToString("N")[..8]);
        _dydoDir = Path.Combine(_testDir, "dydo");
        Directory.CreateDirectory(_dydoDir);

        // Create minimal dydo.json
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            {
                "version": 1,
                "structure": { "root": "dydo" },
                "agents": { "pool": [], "assignments": {} }
            }
            """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
        {
            // Clean up git lock files if any
            try
            {
                Directory.Delete(_testDir, true);
            }
            catch
            {
                // Retry after a short delay
                Thread.Sleep(100);
                try { Directory.Delete(_testDir, true); } catch { }
            }
        }
    }

    private void InitGitRepo()
    {
        RunGit("init");
        RunGit("config user.email \"test@test.com\"");
        RunGit("config user.name \"Test\"");
    }

    private void RunGit(string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = args,
            WorkingDirectory = _testDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        process?.WaitForExit(5000);
    }

    #region CaptureSnapshot Tests

    [Fact]
    public void CaptureSnapshot_ReturnsSnapshotWithGitCommit()
    {
        InitGitRepo();

        // Create a file and commit
        File.WriteAllText(Path.Combine(_testDir, "README.md"), "# Test");
        RunGit("add .");
        RunGit("commit -m \"initial\"");

        var service = new SnapshotService();
        var snapshot = service.CaptureSnapshot(_testDir);

        Assert.NotNull(snapshot);
        Assert.NotEqual("unknown", snapshot.GitCommit);
        Assert.Equal(40, snapshot.GitCommit.Length); // Full hash is 40 chars
    }

    [Fact]
    public void CaptureSnapshot_ReturnsGitTrackedFiles()
    {
        InitGitRepo();

        // Create files in different folders
        Directory.CreateDirectory(Path.Combine(_testDir, "src"));
        File.WriteAllText(Path.Combine(_testDir, "README.md"), "# Test");
        File.WriteAllText(Path.Combine(_testDir, "src", "main.cs"), "// code");

        RunGit("add .");
        RunGit("commit -m \"initial\"");

        var service = new SnapshotService();
        var snapshot = service.CaptureSnapshot(_testDir);

        Assert.Contains("README.md", snapshot.Files);
        Assert.Contains("src/main.cs", snapshot.Files);
        Assert.Contains("dydo.json", snapshot.Files);
    }

    [Fact]
    public void CaptureSnapshot_DerivesFoldersFromFiles()
    {
        InitGitRepo();

        // Create nested structure
        Directory.CreateDirectory(Path.Combine(_testDir, "src", "components"));
        File.WriteAllText(Path.Combine(_testDir, "src", "components", "button.ts"), "// code");

        RunGit("add .");
        RunGit("commit -m \"initial\"");

        var service = new SnapshotService();
        var snapshot = service.CaptureSnapshot(_testDir);

        Assert.Contains("src", snapshot.Folders);
        Assert.Contains("src/components", snapshot.Folders);
    }

    [Fact]
    public void CaptureSnapshot_ExtractsDocLinks()
    {
        InitGitRepo();

        // Create docs with links
        File.WriteAllText(Path.Combine(_dydoDir, "index.md"), """
            ---
            title: Index
            type: hub
            area: general
            ---
            # Index
            See [about](./about.md) for more.
            """);

        File.WriteAllText(Path.Combine(_dydoDir, "about.md"), """
            ---
            title: About
            type: concept
            area: understand
            ---
            # About
            This is about.
            """);

        RunGit("add .");
        RunGit("commit -m \"initial\"");

        var service = new SnapshotService();
        var snapshot = service.CaptureSnapshot(_testDir);

        Assert.True(snapshot.DocLinks.Count > 0);
        Assert.True(snapshot.DocLinks.ContainsKey("dydo/index.md"));
        Assert.Contains("dydo/about.md", snapshot.DocLinks["dydo/index.md"]);
    }

    [Fact]
    public void CaptureSnapshot_HandlesNonGitDirectory()
    {
        // Don't init git
        var service = new SnapshotService();
        var snapshot = service.CaptureSnapshot(_testDir);

        Assert.NotNull(snapshot);
        Assert.Equal("unknown", snapshot.GitCommit);
        Assert.Empty(snapshot.Files);
        Assert.Empty(snapshot.Folders);
    }

    [Fact]
    public void CaptureSnapshot_FilesAreSorted()
    {
        InitGitRepo();

        // Create files in non-alphabetical order
        File.WriteAllText(Path.Combine(_testDir, "zebra.md"), "z");
        File.WriteAllText(Path.Combine(_testDir, "alpha.md"), "a");
        File.WriteAllText(Path.Combine(_testDir, "beta.md"), "b");

        RunGit("add .");
        RunGit("commit -m \"initial\"");

        var service = new SnapshotService();
        var snapshot = service.CaptureSnapshot(_testDir);

        var mdFiles = snapshot.Files.Where(f => f.EndsWith(".md")).ToList();
        Assert.Equal("alpha.md", mdFiles[0]);
        Assert.Equal("beta.md", mdFiles[1]);
        Assert.Equal("zebra.md", mdFiles[2]);
    }

    [Fact]
    public void CaptureSnapshot_FoldersAreSorted()
    {
        InitGitRepo();

        // Create folders in non-alphabetical order
        Directory.CreateDirectory(Path.Combine(_testDir, "zoo"));
        Directory.CreateDirectory(Path.Combine(_testDir, "apple"));
        Directory.CreateDirectory(Path.Combine(_testDir, "banana"));

        File.WriteAllText(Path.Combine(_testDir, "zoo", "file.txt"), "z");
        File.WriteAllText(Path.Combine(_testDir, "apple", "file.txt"), "a");
        File.WriteAllText(Path.Combine(_testDir, "banana", "file.txt"), "b");

        RunGit("add .");
        RunGit("commit -m \"initial\"");

        var service = new SnapshotService();
        var snapshot = service.CaptureSnapshot(_testDir);

        var nonDydoFolders = snapshot.Folders.Where(f => !f.StartsWith("dydo")).ToList();
        Assert.Equal("apple", nonDydoFolders[0]);
        Assert.Equal("banana", nonDydoFolders[1]);
        Assert.Equal("zoo", nonDydoFolders[2]);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CaptureSnapshot_IgnoresUntrackedFiles()
    {
        InitGitRepo();

        // Create and commit one file
        File.WriteAllText(Path.Combine(_testDir, "tracked.md"), "tracked");
        RunGit("add .");
        RunGit("commit -m \"initial\"");

        // Create untracked file
        File.WriteAllText(Path.Combine(_testDir, "untracked.md"), "untracked");

        var service = new SnapshotService();
        var snapshot = service.CaptureSnapshot(_testDir);

        Assert.Contains("tracked.md", snapshot.Files);
        Assert.DoesNotContain("untracked.md", snapshot.Files);
    }

    [Fact]
    public void CaptureSnapshot_HandlesDeepNesting()
    {
        InitGitRepo();

        // Create deeply nested file
        var deepPath = Path.Combine(_testDir, "a", "b", "c", "d", "e");
        Directory.CreateDirectory(deepPath);
        File.WriteAllText(Path.Combine(deepPath, "deep.txt"), "deep");

        RunGit("add .");
        RunGit("commit -m \"initial\"");

        var service = new SnapshotService();
        var snapshot = service.CaptureSnapshot(_testDir);

        Assert.Contains("a/b/c/d/e/deep.txt", snapshot.Files);
        Assert.Contains("a", snapshot.Folders);
        Assert.Contains("a/b", snapshot.Folders);
        Assert.Contains("a/b/c", snapshot.Folders);
        Assert.Contains("a/b/c/d", snapshot.Folders);
        Assert.Contains("a/b/c/d/e", snapshot.Folders);
    }

    [Fact]
    public void CaptureSnapshot_NormalizesPathSeparators()
    {
        InitGitRepo();

        Directory.CreateDirectory(Path.Combine(_testDir, "folder"));
        File.WriteAllText(Path.Combine(_testDir, "folder", "file.txt"), "test");

        RunGit("add .");
        RunGit("commit -m \"initial\"");

        var service = new SnapshotService();
        var snapshot = service.CaptureSnapshot(_testDir);

        // All paths should use forward slashes
        Assert.All(snapshot.Files, f => Assert.DoesNotContain("\\", f));
        Assert.All(snapshot.Folders, f => Assert.DoesNotContain("\\", f));
    }

    #endregion
}
