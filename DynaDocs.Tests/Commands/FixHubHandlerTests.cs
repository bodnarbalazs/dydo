namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Services;

public class FixHubHandlerTests : IDisposable
{
    private readonly string _basePath;

    public FixHubHandlerTests()
    {
        _basePath = Path.Combine(Path.GetTempPath(), "fix-hub-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_basePath);
    }

    public void Dispose()
    {
        if (Directory.Exists(_basePath)) Directory.Delete(_basePath, recursive: true);
    }

    [Fact]
    public void RegenerateHubs_DeletesAutoGenTasksIndex()
    {
        var tasksDir = Path.Combine(_basePath, "project", "tasks");
        Directory.CreateDirectory(tasksDir);
        var indexPath = Path.Combine(tasksDir, "_index.md");
        File.WriteAllText(indexPath,
            "---\narea: project\ntype: hub\n---\n\n" + HubGenerator.AutoGenComment + "\n\n# Tasks\n");

        File.WriteAllText(Path.Combine(tasksDir, "_tasks.md"),
            "---\narea: project\ntype: folder-meta\n---\n\n# Tasks\nMeta.\n");

        var scanner = new DocScanner(new MarkdownParser());
        var docs = scanner.ScanDirectory(_basePath);

        FixHubHandler.RegenerateHubs(_basePath, scanner, docs);

        Assert.False(File.Exists(indexPath));
    }

    [Fact]
    public void RegenerateHubs_PreservesHandWrittenTasksIndex()
    {
        var tasksDir = Path.Combine(_basePath, "project", "tasks");
        Directory.CreateDirectory(tasksDir);
        var indexPath = Path.Combine(tasksDir, "_index.md");
        var handWritten = "---\narea: project\ntype: hub\n---\n\n# Custom hand-written index\n";
        File.WriteAllText(indexPath, handWritten);

        File.WriteAllText(Path.Combine(tasksDir, "_tasks.md"),
            "---\narea: project\ntype: folder-meta\n---\n\n# Tasks\nMeta.\n");

        var scanner = new DocScanner(new MarkdownParser());
        var docs = scanner.ScanDirectory(_basePath);

        FixHubHandler.RegenerateHubs(_basePath, scanner, docs);

        Assert.True(File.Exists(indexPath));
        Assert.Equal(handWritten, File.ReadAllText(indexPath));
    }
}
