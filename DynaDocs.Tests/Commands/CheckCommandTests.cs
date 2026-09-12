namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;

[Collection("Integration")]
public class CheckCommandTests : IDisposable
{
    private readonly string _root;
    private readonly string _original;

    public CheckCommandTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "dydo-check-config-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _original = Environment.CurrentDirectory;
        Environment.CurrentDirectory = _root;
    }

    [Fact]
    public void Check_MissingRequiredScanExclusionFailsInsteadOfReportingSuccess()
    {
        File.WriteAllText(Path.Combine(_root, "dydo.json"),
            "{\"version\":1,\"structure\":{\"root\":\"dydo\"},\"scanExclude\":[\"_system/templates/\"]}");
        Directory.CreateDirectory(Path.Combine(_root, "dydo"));

        var (code, stdout, stderr) = ConsoleCapture.All(() => CheckCommand.Create().Parse("").Invoke());

        Assert.NotEqual(0, code);
        Assert.Contains("_system/.local/", stderr);
        Assert.DoesNotContain("All checks passed", stdout);
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _original;
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }
}
