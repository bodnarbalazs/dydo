namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public sealed class WorktreeProcessProbeTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dydo-probe-" + Guid.NewGuid().ToString("N")[..8]);

    public WorktreeProcessProbeTests() => Directory.CreateDirectory(_root);

    public void Dispose() => LinkSafeDelete.DeleteTree(_root);

    [Fact]
    public void RenameProbe_IdleFolder_IsNotInUse_AndStaysWhereItWas()
    {
        var tree = Directory.CreateDirectory(Path.Combine(_root, "tree")).FullName;
        File.WriteAllText(Path.Combine(tree, "file.txt"), "x");

        Assert.Null(WorktreeProcessProbe.RenameProbe(tree));
        Assert.True(File.Exists(Path.Combine(tree, "file.txt")));
        Assert.Single(Directory.GetDirectories(_root));
    }

    [Fact]
    public void RenameProbe_FolderThatCannotBeRenamed_ReportsInUse()
    {
        Assert.Contains("cannot be renamed", WorktreeProcessProbe.RenameProbe(Path.Combine(_root, "missing")));
    }

    [Fact]
    public void ProcScan_FindsAProcessWhoseWorkingDirectoryIsInside()
    {
        var tree = Directory.CreateDirectory(Path.Combine(_root, "tree")).FullName;
        var inside = Directory.CreateDirectory(Path.Combine(tree, "src")).FullName;
        var elsewhere = Directory.CreateDirectory(Path.Combine(_root, "elsewhere")).FullName;
        var proc = Path.Combine(_root, "proc");
        DirectoryLinks.Create(Path.Combine(proc, "100", "cwd"), elsewhere);
        DirectoryLinks.Create(Path.Combine(proc, "200", "cwd"), inside);
        Directory.CreateDirectory(Path.Combine(proc, "self"));

        Assert.Equal("a running process (pid 200) has its working directory inside",
            WorktreeProcessProbe.ProcScan(tree, proc));
    }

    [Fact]
    public void ProcScan_IgnoresUnreadableAndUnrelatedProcesses()
    {
        var tree = Directory.CreateDirectory(Path.Combine(_root, "tree")).FullName;
        var sibling = Directory.CreateDirectory(Path.Combine(_root, "tree-sibling")).FullName;
        var proc = Path.Combine(_root, "proc");
        DirectoryLinks.Create(Path.Combine(proc, "100", "cwd"), sibling);
        Directory.CreateDirectory(Path.Combine(proc, "300"));
        Directory.CreateDirectory(Path.Combine(proc, Environment.ProcessId.ToString()));

        Assert.Null(WorktreeProcessProbe.ProcScan(tree, proc));
    }
}
