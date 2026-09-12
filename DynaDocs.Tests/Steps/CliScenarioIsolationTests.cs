namespace DynaDocs.Tests.Steps;

public class CliScenarioIsolationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("/bin/bash")]
    [InlineData("/bin/zsh")]
    [InlineData("C:\\Git\\bin\\bash")]
    [InlineData("C:\\bin\\zsh")]
    public void ChildEnvironment_DisablesProfileInstallationWithoutChangingParent(string? shell)
    {
        var inherited = new Dictionary<string, string?>
        {
            ["PATH"] = "test-path",
            ["DYDO_WINDOW"] = "parent-window"
        };
        if (shell != null)
            inherited.Add("SHELL", shell);
        var original = inherited.ToArray();

        var start = CliScenario.CreateStartInfo("scratch", ["init", "all"], inherited);

        Assert.Equal("dydo-acceptance-no-shell", start.Environment["SHELL"]);
        Assert.DoesNotContain("DYDO_WINDOW", start.Environment.Keys);
        Assert.Equal("test-path", start.Environment["PATH"]);
        Assert.Equal(original, inherited.ToArray());
        Assert.Equal(new[] { "init", "all" }, start.ArgumentList);
    }
}
