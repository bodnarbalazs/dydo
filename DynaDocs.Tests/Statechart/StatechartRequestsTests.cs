namespace DynaDocs.Tests.Statechart;

using DynaDocs.Services.Statechart;

public class StatechartRequestsTests
{
    [Fact]
    public void PendingRequestSurvivesFailureAndRejectsConcurrentExecutionOrTampering()
    {
        using var scope = new StatechartTestDirectory();
        var assets = new StatechartAssets();
        var home = new StatechartHome(Path.Combine(scope.Path, "assets"), Path.Combine(scope.Path, "deps"), Path.Combine(scope.Path, "state"));
        var store = new StatechartRequests(home, assets, "win-x64");
        File.WriteAllText(Path.Combine(scope.Path, "model.tla"), "model");
        File.WriteAllText(Path.Combine(scope.Path, "model.cfg"), "config");
        var original = StatechartRequest.Capture(["model.tla"], scope.Path, assets, "win-x64");
        var path = store.Save(original);
        Assert.Equal(original.Id, store.Load(path).Id);
        using (store.Lock(path)) Assert.Throws<IOException>(() => store.Lock(path));
        using (store.Lock(path)) store.Failure(path, "host continuation failed; no consent assumed");
        Assert.Equal(original.Id, store.Load(path).Id);
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(path)!, "failure-*.json"));
        File.AppendAllText(path, " ");
        Assert.Throws<IOException>(() => store.Load(path));
        Assert.True(File.Exists(path));
        Assert.False(Directory.Exists(home.Dependencies));
    }

    [Fact]
    public void CompletedRequestReturnsRecordedVerdictAndRejectsChangedOutputWithoutRerunning()
    {
        using var scope = new StatechartTestDirectory();
        var assets = new StatechartAssets();
        var home = new StatechartHome(scope.Path, Path.Combine(scope.Path, "deps"), Path.Combine(scope.Path, "state"));
        var store = new StatechartRequests(home, assets, "win-x64");
        File.WriteAllText(Path.Combine(scope.Path, "model.tla"), "model");
        File.WriteAllText(Path.Combine(scope.Path, "model.cfg"), "config");
        var request = StatechartRequest.Capture(["model.tla"], scope.Path, assets, "win-x64");
        var path = store.Save(request);
        Assert.Null(store.Completed(path, request));
        Directory.CreateDirectory(request.Output);
        var report = Path.Combine(request.Output, "report.md");
        File.WriteAllText(report, "real report fixture");
        Assert.Throws<InvalidOperationException>(() => store.Complete(path, request, 2));
        store.Complete(path, request, 1);
        var reloaded = store.Load(path, validateInputs: false);
        Assert.Equal(1, store.Completed(path, reloaded));
        File.WriteAllText(report, "changed");
        Assert.Throws<IOException>(() => store.Completed(path, reloaded));
        Assert.Equal("changed", File.ReadAllText(report));
    }

    [Fact]
    public void ResumeCannotChooseAnotherStateOrDependencyRoot()
    {
        using var scope = new StatechartTestDirectory();
        var assets = new StatechartAssets();
        var home = new StatechartHome(scope.Path, Path.Combine(scope.Path, "deps"), Path.Combine(scope.Path, "state"));
        var first = new StatechartRequests(home, assets, "win-x64");
        File.WriteAllText(Path.Combine(scope.Path, "model.tla"), "model");
        File.WriteAllText(Path.Combine(scope.Path, "model.cfg"), "config");
        var path = first.Save(StatechartRequest.Capture(["model.tla"], scope.Path, assets, "win-x64"));
        Assert.Throws<IOException>(() => new StatechartRequests(home with { Dependencies = Path.Combine(scope.Path, "other") }, assets, "win-x64").Load(path));
        Assert.Throws<IOException>(() => new StatechartRequests(home with { State = Path.Combine(scope.Path, "other-state") }, assets, "win-x64").Load(path));
        Assert.Throws<IOException>(() => first.Load("request.json"));
    }
}
