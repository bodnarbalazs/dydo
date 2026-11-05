namespace DynaDocs.Tests.Steps;

using System.Text.Json.Nodes;
using Reqnroll;

[Binding]
public class ClaudeGuardWiringSteps(CliScenario scenario)
{
    private string SettingsPath => Path.Combine(scenario.DirectoryPath, ".claude", "settings.json");

    [Given("the Claude settings no longer run the dydo guard")]
    public void DropGuardHook()
    {
        var settings = JsonNode.Parse(File.ReadAllText(SettingsPath))!.AsObject();
        Assert.True(settings.Remove("hooks"), $"Expected init to wire hooks in {SettingsPath}");
        File.WriteAllText(SettingsPath, settings.ToJsonString());
    }

    [When("I validate the project")]
    public Task Validate() => scenario.RunAsync("validate");

    [When("I rejoin dydo with {string}")]
    public Task Rejoin(string integration) => scenario.RunAsync("init", integration, "--join");

    [Then("validation passes with exactly this warning:")]
    public void PassesWithWarning(Table expected)
    {
        var result = scenario.Result;
        var output = $"Exit: {result.ExitCode}\n{result.Stdout}\n{result.Stderr}";
        Assert.True(expected.Header.SequenceEqual(["file", "warning"]), output);
        var row = Assert.Single(expected.Rows);
        Assert.True(result.ExitCode == 0 && result.Stdout == "", output);
        Assert.Equal($"Warnings (1):\n  {row["file"]}: {row["warning"]}\n", result.Stderr.ReplaceLineEndings("\n"));
    }

    [Then("validation passes with no issues")]
    public void PassesClean()
    {
        var result = scenario.Result;
        Assert.True(result.ExitCode == 0 && result.Stderr == "", $"Exit: {result.ExitCode}\n{result.Stdout}\n{result.Stderr}");
        Assert.Equal("Validation passed. No issues found.\n", result.Stdout.ReplaceLineEndings("\n"));
    }
}
