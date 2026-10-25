using System.Text.Json.Nodes;
using DynaDocs.Commands;
using DynaDocs.Utils;

namespace DynaDocs.Tests.Integration;

[Collection("Integration")]
public class AdoptCommandTests : IntegrationTestBase
{
    private string ConfigPath => Path.Combine(TestDir, "dydo.json");

    [Theory]
    [InlineData("3.6.0")]
    [InlineData("3.6.0-beta.3")]
    public async Task Adopt_RecordsTheVersion(string version)
    {
        (await InitProjectAsync("codex")).AssertSuccess();
        SetDydoVersion("1.2.3");

        var result = await AdoptAsync(version);

        result.AssertSuccess();
        Assert.Contains($"Recorded dydoVersion {version} in dydo.json", result.Stdout);
        Assert.Equal(version, ReadDydoVersion());
        Assert.True(ReadConfigJson()["integrations"]!["codex"]!.GetValue<bool>());
        Assert.Empty(Directory.GetFiles(TestDir, "dydo.json.*.tmp"));
    }

    [Fact]
    public async Task Adopt_LeadingV_IsStripped()
    {
        (await InitProjectAsync("none")).AssertSuccess();

        (await AdoptAsync("v3.6.0")).AssertSuccess();

        Assert.Equal("3.6.0", ReadDydoVersion());
    }

    [Fact]
    public async Task Adopt_ProjectWithoutDydoVersion_GainsIt()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        SetDydoVersion(null);

        (await AdoptAsync("3.6.0")).AssertSuccess();

        Assert.Equal("3.6.0", ReadDydoVersion());
    }

    [Theory]
    [InlineData("latest")]
    [InlineData("3.6")]
    [InlineData("vv3.6.0")]
    [InlineData("3.6.0 extra")]
    [InlineData("")]
    public async Task Adopt_InvalidVersion_FailsAndLeavesConfigBytes(string version)
    {
        (await InitProjectAsync("none")).AssertSuccess();
        var original = File.ReadAllBytes(ConfigPath);

        var result = await AdoptAsync(version);

        result.AssertExitCode(ExitCodes.ToolError);
        Assert.Contains("is not a dydo release", result.Stderr);
        Assert.Equal(original, File.ReadAllBytes(ConfigPath));
    }

    [Fact]
    public async Task Adopt_NoProject_FailsWithoutCreatingConfig()
    {
        var result = await AdoptAsync("3.6.0");

        result.AssertExitCode(ExitCodes.ToolError);
        Assert.Contains("No DynaDocs project found", result.Stderr);
        Assert.False(File.Exists(ConfigPath));
    }

    [Fact]
    public async Task Adopt_UnparseableConfig_FailsAndLeavesItAlone()
    {
        File.WriteAllText(ConfigPath, "{ not json");

        var result = await AdoptAsync("3.6.0");

        result.AssertExitCode(ExitCodes.ToolError);
        Assert.Equal("{ not json", File.ReadAllText(ConfigPath));
    }

    private Task<CommandResult> AdoptAsync(string version) => RunAsync(AdoptCommand.Create(), version);

    private JsonObject ReadConfigJson() => JsonNode.Parse(File.ReadAllText(ConfigPath))!.AsObject();

    private string? ReadDydoVersion() => ReadConfigJson()["dydoVersion"]?.GetValue<string>();

    private void SetDydoVersion(string? version)
    {
        var raw = ReadConfigJson();
        raw.Remove("dydoVersion");
        if (version != null)
            raw["dydoVersion"] = version;
        File.WriteAllText(ConfigPath, raw.ToJsonString());
    }
}
