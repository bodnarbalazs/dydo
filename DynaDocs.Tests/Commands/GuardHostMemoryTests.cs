namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;

/// <summary>
/// The host memory folders DR 055 closes to agent writes, matched on plain paths.
/// </summary>
public class GuardHostMemoryTests : IDisposable
{
    private const string Home = "C:/Users/u";

    private static readonly GuardCommand.HostMemoryFolders Folders = new(
        Home, "D:/profiles/work", "E:/codex-home", ["F:/custom-memory"]);

    private readonly string _root = Path.Combine(Path.GetTempPath(), "dydo-host-memory-" + Guid.NewGuid().ToString("N")[..8]);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    [Theory]
    [InlineData("C:/Users/u/.claude/projects/my-proj/memory/MEMORY.md")]
    [InlineData("C:\\Users\\u\\.claude\\projects\\my-proj\\memory\\topics\\api.md")]
    [InlineData("C:/Users/u/.claude/projects/my-proj/memory")]
    [InlineData("C:/Users/u/.claude-personal2/projects/p/memory/MEMORY.md")]
    [InlineData("C:/Users/u/.claude/agent-memory/reviewer/MEMORY.md")]
    [InlineData("C:/Users/u/.claude-personal2/agent-memory-local/x.md")]
    [InlineData("D:/profiles/work/projects/p/memory/MEMORY.md")]
    [InlineData("D:/profiles/work/agent-memory/reviewer/MEMORY.md")]
    [InlineData("C:/repo/.claude/agent-memory/reviewer/MEMORY.md")]
    [InlineData(".claude/agent-memory-local/reviewer/MEMORY.md")]
    [InlineData("E:/codex-home/memories/note.md")]
    [InlineData("E:/codex-home/memories")]
    [InlineData("F:/custom-memory/MEMORY.md")]
    [InlineData("~/.claude/projects/p/memory/MEMORY.md")]
    [InlineData("C:/Users/u/.claude/projects/p/other/../memory/MEMORY.md")]
    public void Contains_HostMemoryFolders(string path)
    {
        Assert.True(Folders.Contains(path));
    }

    [Theory]
    [InlineData("C:/Users/u/.claude/projects/p/memory/../../../secret.md")]   // '..' escape out
    [InlineData("C:/Users/other/.claude/projects/p/memory/MEMORY.md")]        // another home
    [InlineData("dydo/.claude/projects/x/memory/y.md")]                       // repo lookalike
    [InlineData("dydo/scratch/memory/notes.md")]
    [InlineData("C:/Users/u/.claude/settings.json")]
    [InlineData("C:/Users/u/.claude.json")]
    [InlineData("C:/Users/u/.claude/projects/p/session.jsonl")]
    [InlineData("C:/Users/u/.codex/memories/note.md")]                        // CODEX_HOME moved it
    [InlineData("E:/codex-home/config.toml")]
    [InlineData("F:/custom-memory-other/MEMORY.md")]
    [InlineData("src/Foo.cs")]
    public void Contains_OtherPaths_DoNotMatch(string path)
    {
        Assert.False(Folders.Contains(path));
    }

    [Fact]
    public void Contains_GitBashDriveMount_Matches()
    {
        if (!OperatingSystem.IsWindows()) return; // Git Bash drive mounts are a Windows form
        Assert.True(Folders.Contains("/c/Users/u/.claude/projects/p/memory/MEMORY.md"));
    }

    [Fact]
    public void Load_ReadsAutoMemoryDirectoryFromProjectSettings()
    {
        var memoryDir = Path.Combine(_root, "notes-memory").Replace('\\', '/');
        Directory.CreateDirectory(Path.Combine(_root, ".claude"));
        File.WriteAllText(Path.Combine(_root, ".claude", "settings.local.json"),
            $$"""{ "autoMemoryDirectory": "{{memoryDir}}" }""");

        var folders = GuardCommand.HostMemoryFolders.Load(_root);

        Assert.True(folders.Contains($"{memoryDir}/MEMORY.md"));
        Assert.False(folders.Contains($"{_root.Replace('\\', '/')}/src/Foo.cs"));
    }

    [Fact]
    public void Load_UnreadableSettings_NamesNoFolder()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".claude"));
        File.WriteAllText(Path.Combine(_root, ".claude", "settings.json"), "{ not json");

        var folders = GuardCommand.HostMemoryFolders.Load(_root);

        Assert.False(folders.Contains($"{_root.Replace('\\', '/')}/src/Foo.cs"));
    }
}
