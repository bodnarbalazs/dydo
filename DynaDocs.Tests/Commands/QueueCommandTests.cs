namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using DynaDocs.Services;

[Collection("Integration")]
public class QueueCommandTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _originalDir;

    public QueueCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-queuecmd-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _originalDir = Environment.CurrentDirectory;

        File.WriteAllText(Path.Combine(_testDir, "dydo.json"),
            """{"version":1,"structure":{"root":"dydo","tasks":"project/tasks","issues":"project/issues"},"paths":{"source":[],"tests":[],"pathSets":null},"agents":{"pool":["Adele","Brian","Charlie"],"assignments":{"testuser":["Adele","Brian","Charlie"]}},"integrations":{"claude":false},"dispatch":{"launchInTab":false,"autoClose":false},"queues":["merge"],"tasks":{"autoCompactInterval":20},"frameworkHashes":{}}""");

        var localDir = Path.Combine(_testDir, "dydo", "_system", ".local");
        Directory.CreateDirectory(localDir);

        // Create agent workspaces
        foreach (var agent in new[] { "Adele", "Brian", "Charlie" })
            Directory.CreateDirectory(Path.Combine(_testDir, "dydo", "agents", agent));

        Environment.CurrentDirectory = _testDir;
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDir;
        if (Directory.Exists(_testDir))
        {
            try { Directory.Delete(_testDir, true); } catch { }
        }
    }

    [Fact]
    public async Task Show_DisplaysPersistentQueue()
    {
        var (stdout, _, exitCode) = await RunQueueCommand("show");
        Assert.Equal(0, exitCode);
        Assert.Contains("merge", stdout);
        Assert.Contains("persistent", stdout);
    }

    [Fact]
    public async Task Show_SpecificQueue_DisplaysState()
    {
        var (stdout, _, exitCode) = await RunQueueCommand("show", "merge");
        Assert.Equal(0, exitCode);
        Assert.Contains("merge", stdout);
        Assert.Contains("Active: (none)", stdout);
    }

    [Fact]
    public async Task Show_NonExistentQueue_ReturnsError()
    {
        var (_, stderr, exitCode) = await RunQueueCommand("show", "nonexistent");
        Assert.Equal(2, exitCode);
        Assert.Contains("No queue 'nonexistent'", stderr);
    }

    [Fact]
    public async Task Create_TransientQueue_Succeeds()
    {
        var (stdout, _, exitCode) = await RunQueueCommand("create", "hotfix");
        Assert.Equal(0, exitCode);
        Assert.Contains("hotfix", stdout);
        Assert.Contains("transient", stdout);
    }

    [Fact]
    public async Task Create_PersistentQueue_ReturnsError()
    {
        var (_, stderr, exitCode) = await RunQueueCommand("create", "merge");
        Assert.Equal(2, exitCode);
        Assert.Contains("persistent", stderr);
    }

    [Fact]
    public async Task Clear_EmptyQueue_Succeeds()
    {
        // Create the queue directory for merge (persistent queues don't auto-create dirs)
        Directory.CreateDirectory(Path.Combine(_testDir, "dydo", "_system", ".local", "queues", "merge"));

        var (stdout, _, exitCode) = await RunQueueCommand("clear", "merge");
        Assert.Equal(0, exitCode);
        Assert.Contains("cleared", stdout);
    }

    [Fact]
    public async Task Clear_NonExistentQueue_ReturnsError()
    {
        var (_, stderr, exitCode) = await RunQueueCommand("clear", "nonexistent");
        Assert.Equal(2, exitCode);
        Assert.Contains("not found", stderr);
    }

    [Fact]
    public async Task Show_WithActiveAndPending_DisplaysBoth()
    {
        var service = new QueueService(Path.Combine(_testDir, "dydo"));

        // Set up active + pending items
        service.SetActive("merge", "Adele", "task-1", Environment.ProcessId);
        service.TryAcquireOrEnqueue("merge", "Brian", "task-2", true, false, null, null, null, null, null);

        var (stdout, _, exitCode) = await RunQueueCommand("show", "merge");
        Assert.Equal(0, exitCode);
        Assert.Contains("Adele", stdout);
        Assert.Contains("task-1", stdout);
        Assert.Contains("running", stdout);
        Assert.Contains("Pending: 1", stdout);
        Assert.Contains("Brian", stdout);
    }

    [Fact]
    public async Task Show_TransientQueue_DisplaysType()
    {
        var service = new QueueService(Path.Combine(_testDir, "dydo"));
        service.CreateQueue("hotfix", out _);

        var (stdout, _, exitCode) = await RunQueueCommand("show", "hotfix");
        Assert.Equal(0, exitCode);
        Assert.Contains("transient", stdout);
    }

    [Fact]
    public async Task Cancel_NonExistentEntry_ReturnsError()
    {
        Directory.CreateDirectory(Path.Combine(_testDir, "dydo", "_system", ".local", "queues", "merge"));

        var (_, stderr, exitCode) = await RunQueueCommand("cancel", "merge", "9999");
        Assert.Equal(2, exitCode);
        Assert.Contains("No pending entry", stderr);
    }

    private async Task<(string Stdout, string Stderr, int ExitCode)> RunQueueCommand(params string[] args)
    {
        var command = QueueCommand.Create();
        var (exitCode, stdout, stderr) = await ConsoleCapture.AllAsync(
            async () => await command.Parse(args).InvokeAsync());
        return (stdout, stderr, exitCode);
    }
}
