namespace DynaDocs.Tests.Steps;

using DynaDocs.Commands;
using DynaDocs.Services;
using Reqnroll;

/// <summary>
/// Mode store faults built from owned scratch only, the same on every OS and for every user: a mode
/// file held by an exclusive lock, or a file standing where the store folder must be created. Native
/// permission denial lives in ModeCommandTests. The working directory is left alone, so the machine
/// mode steps' unscoped hook can share these scenarios.
/// </summary>
[Binding, Scope(Tag = "mode-store-failure")]
public sealed class ModeStoreFailureSteps
{
    private readonly string _scratch = Path.Combine(Path.GetTempPath(), "dydo-mode-store-" + Guid.NewGuid().ToString("N"));
    private string StoreFolder => Path.Combine(_scratch, "machine");
    private string StorePath => Path.Combine(StoreFolder, "mode.json");

    private FileStream? _lock;
    private string _cause = "";
    private Dictionary<string, string> _filesBefore = [];
    private string _shownBefore = "";
    private (int ExitCode, string Stdout, string Stderr) _result;

    [AfterScenario]
    public void Restore()
    {
        _lock?.Dispose();
        if (Directory.Exists(_scratch))
            Directory.Delete(_scratch, recursive: true);
    }

    [Given("the owned mode store holds {string} in a locked mode file")]
    public async Task LockedModeFile(string value)
    {
        Assert.Equal(0, (await Run("set", "overnight", value)).ExitCode);
        await Snapshot();
        _lock = new FileStream(StorePath, FileMode.Open, FileAccess.Read, FileShare.None);
        _cause = Assert.ThrowsAny<IOException>(() => new FileStream(StorePath, FileMode.Open, FileAccess.Write, FileShare.Read)).Message;
    }

    [Given("the owned mode store folder's place is taken by a file")]
    public async Task FolderTakenByAFile()
    {
        Directory.CreateDirectory(_scratch);
        File.WriteAllText(StoreFolder, "not a folder");
        _cause = Assert.ThrowsAny<IOException>(() => Directory.CreateDirectory(StoreFolder)).Message;
        await Snapshot();
    }

    [When("I set the owned overnight mode to {string}")]
    public async Task Set(string value) => _result = await Run("set", "overnight", value);

    [Then("the mode set fails with one error naming the mode store path and its cause")]
    public void FailsWithOneError()
    {
        Assert.Equal(2, _result.ExitCode);
        Assert.Equal("", _result.Stdout);
        Assert.Equal($"Cannot write the mode store {StorePath}: {_cause}{Environment.NewLine}", _result.Stderr);
    }

    [Then("the owned mode store is as it was")]
    public async Task StoreAsItWas()
    {
        _lock?.Dispose();
        Assert.Equal(_filesBefore, Files());
        Assert.Equal(_shownBefore, await Shown());
    }

    private Task<(int ExitCode, string Stdout, string Stderr)> Run(params string[] args) =>
        ConsoleCapture.AllAsync(() => ModeCommand.Create(new ModeState(StoreFolder)).Parse(args).InvokeAsync());

    private async Task Snapshot()
    {
        _filesBefore = Files();
        _shownBefore = await Shown();
    }

    private Dictionary<string, string> Files() =>
        Directory.EnumerateFiles(_scratch, "*", SearchOption.AllDirectories).ToDictionary(path => path, File.ReadAllText);

    private async Task<string> Shown()
    {
        var shown = await Run("show");
        Assert.Equal(0, shown.ExitCode);
        return shown.Stdout;
    }
}
