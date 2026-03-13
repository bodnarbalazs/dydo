namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class InboxMetadataReaderTests : IDisposable
{
    private readonly string _testDir;
    private readonly InboxMetadataReader _reader;

    public InboxMetadataReaderTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-inbox-reader-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _reader = new InboxMetadataReader(agent => Path.Combine(_testDir, agent));
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private void CreateInboxFile(string agent, string filename, string content)
    {
        var inboxDir = Path.Combine(_testDir, agent, "inbox");
        Directory.CreateDirectory(inboxDir);
        File.WriteAllText(Path.Combine(inboxDir, filename), content);
    }

    [Fact]
    public void GetDispatchedRole_NoInboxDir_ReturnsNull()
    {
        Assert.Null(_reader.GetDispatchedRole("NoAgent", "some-task"));
    }

    [Fact]
    public void GetDispatchedRole_NoMatchingFile_ReturnsNull()
    {
        var inboxDir = Path.Combine(_testDir, "Alice", "inbox");
        Directory.CreateDirectory(inboxDir);

        Assert.Null(_reader.GetDispatchedRole("Alice", "nonexistent-task"));
    }

    [Fact]
    public void GetDispatchedRole_ValidFile_ReturnsRole()
    {
        CreateInboxFile("Alice", "abc123-my-task.md", """
            ---
            role: code-writer
            from: Brian
            task: my-task
            ---
            # Brief
            """);

        var role = _reader.GetDispatchedRole("Alice", "my-task");

        Assert.Equal("code-writer", role);
    }

    [Fact]
    public void GetDispatchedFrom_ValidFile_ReturnsFrom()
    {
        CreateInboxFile("Alice", "abc123-my-task.md", """
            ---
            role: code-writer
            from: Brian
            task: my-task
            ---
            # Brief
            """);

        var from = _reader.GetDispatchedFrom("Alice", "my-task");

        Assert.Equal("Brian", from);
    }

    [Fact]
    public void GetDispatchedRole_NoFrontmatter_ReturnsNull()
    {
        CreateInboxFile("Alice", "abc123-my-task.md", "No frontmatter here");

        Assert.Null(_reader.GetDispatchedRole("Alice", "my-task"));
    }

    [Fact]
    public void GetDispatchedRole_UnclosedFrontmatter_ReturnsNull()
    {
        CreateInboxFile("Alice", "abc123-my-task.md", "---\nrole: writer\nno closing");

        Assert.Null(_reader.GetDispatchedRole("Alice", "my-task"));
    }

    [Fact]
    public void GetDispatchedRole_FieldNotPresent_ReturnsNull()
    {
        CreateInboxFile("Alice", "abc123-my-task.md", """
            ---
            from: Brian
            task: my-task
            ---
            # Brief
            """);

        Assert.Null(_reader.GetDispatchedRole("Alice", "my-task"));
    }
}
