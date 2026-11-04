namespace DynaDocs.Tests.Statechart;

using DynaDocs.Services.Statechart;

public class StatechartLockTests
{
    [Fact]
    public async Task AWaiterTakesTheLockOnceItsHolderReleasesIt()
    {
        using var scope = new StatechartTestDirectory();
        var path = Path.Combine(scope.Path, "x.lock");
        var held = Hold(path);
        var waiting = StatechartLock.AcquireAsync(path, TimeSpan.FromMinutes(1), CancellationToken.None);
        Assert.False(waiting.IsCompleted);
        held.Dispose();
        await using var acquired = await waiting;
        Assert.Throws<IOException>(() => Hold(path));
    }

    [Fact]
    public async Task AHolderPastTheBoundIsReportedByItsPath()
    {
        using var scope = new StatechartTestDirectory();
        var path = Path.Combine(scope.Path, "x.lock");
        using var held = Hold(path);
        var error = await Assert.ThrowsAsync<IOException>(() => StatechartLock.AcquireAsync(path, TimeSpan.Zero, CancellationToken.None));
        Assert.Contains(path, error.Message);
    }

    [Fact]
    public async Task CancellationEndsTheWait()
    {
        using var scope = new StatechartTestDirectory();
        var path = Path.Combine(scope.Path, "x.lock");
        using var held = Hold(path);
        using var cancel = new CancellationTokenSource();
        var waiting = StatechartLock.AcquireAsync(path, TimeSpan.FromMinutes(1), cancel.Token);
        await cancel.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }

    [Fact]
    public async Task AMissingDirectoryIsAnErrorNotAWait()
    {
        using var scope = new StatechartTestDirectory();
        await Assert.ThrowsAsync<DirectoryNotFoundException>(() =>
            StatechartLock.AcquireAsync(Path.Combine(scope.Path, "missing", "x.lock"), TimeSpan.FromMinutes(1), CancellationToken.None));
    }

    private static FileStream Hold(string path) => new(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
}
