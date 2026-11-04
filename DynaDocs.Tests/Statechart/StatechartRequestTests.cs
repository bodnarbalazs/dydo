namespace DynaDocs.Tests.Statechart;

using DynaDocs.Services.Statechart;

public class StatechartRequestTests
{
    [Fact]
    public void CapturePreservesCallerArgumentsAndPinsTopLevelInputsWithoutCopyingModels()
    {
        using var scope = new StatechartTestDirectory();
        File.WriteAllText(Path.Combine(scope.Path, "spaced model.tla"), "---- MODULE Model ----\n====");
        File.WriteAllText(Path.Combine(scope.Path, "spaced model.cfg"), "SPECIFICATION Spec");
        var arguments = new[] { "spaced model.tla", "--out", "chart output" };
        var request = StatechartRequest.Capture(arguments, scope.Path, new StatechartAssets(), "win-x64");
        Assert.Equal(arguments, request.Arguments);
        Assert.Equal(scope.Path, request.Caller);
        Assert.Equal(Path.Combine(scope.Path, "spaced model.tla"), request.Spec.Path);
        Assert.Equal(Path.Combine(scope.Path, "spaced model.cfg"), request.Config.Path);
        Assert.Equal(Path.Combine(scope.Path, "chart output"), request.Output);
        Assert.Equal(64, request.Spec.Sha256.Length);
        Assert.False(Directory.Exists(request.Output));
        StatechartRequest.Validate(request, new StatechartAssets(), "win-x64");
    }

    [Theory]
    [InlineData("model.tla")]
    [InlineData("model.cfg")]
    [InlineData("view.ts")]
    public void ApprovalDelayRejectsChangedTopLevelInputs(string changed)
    {
        using var scope = new StatechartTestDirectory();
        foreach (var file in new[] { "model.tla", "model.cfg", "view.ts" })
            File.WriteAllText(Path.Combine(scope.Path, file), "original");
        var assets = new StatechartAssets();
        var request = StatechartRequest.Capture(["model.tla", "--view", "view.ts"], scope.Path, assets, "win-x64");
        File.WriteAllText(Path.Combine(scope.Path, changed), "changed during approval");
        Assert.Throws<IOException>(() => StatechartRequest.Validate(request, assets, "win-x64"));
    }

    [Fact]
    public void ResumeRejectsTamperedArgumentsIdentitiesAndForeignNonemptyOutput()
    {
        using var scope = new StatechartTestDirectory();
        File.WriteAllText(Path.Combine(scope.Path, "model.tla"), "model");
        File.WriteAllText(Path.Combine(scope.Path, "model.cfg"), "config");
        var assets = new StatechartAssets();
        var request = StatechartRequest.Capture(["model.tla"], scope.Path, assets, "win-x64");
        Assert.Throws<IOException>(() => StatechartRequest.Validate(request with { Output = scope.Path }, assets, "win-x64"));
        Assert.Throws<IOException>(() => StatechartRequest.Validate(request with { Protocol = 2 }, assets, "win-x64"));
        Assert.Throws<IOException>(() => StatechartRequest.Validate(request with { PayloadDigest = "tampered" }, assets, "win-x64"));
        Assert.Throws<IOException>(() => StatechartRequest.Validate(request, assets, "linux-x64"));
        Directory.CreateDirectory(request.Output);
        var sentinel = Path.Combine(request.Output, "foreign");
        File.WriteAllText(sentinel, "keep");
        Assert.Throws<IOException>(() => StatechartRequest.Validate(request, assets, "win-x64"));
        Assert.Equal("keep", File.ReadAllText(sentinel));
    }

    [Theory]
    [InlineData("--approved")]
    [InlineData("--unknown")]
    public void CaptureRejectsInventedConsentOrUnknownFlags(string flag)
    {
        using var scope = new StatechartTestDirectory();
        Assert.Throws<IOException>(() => StatechartRequest.Capture(["model.tla", flag], scope.Path,
            new StatechartAssets(), "win-x64"));
    }
}
