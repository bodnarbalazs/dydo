using System.Text.Json.Nodes;
using DynaDocs.Commands;
using DynaDocs.Services;
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

    #region 3.5.0 migration: the docs-outside-dydo notice (DYD-375)

    private const string DocsMessage = "Docs usually live under `dydo/`. If this is a pointer README, or it belongs here, carry on.";

    [Theory]
    [InlineData("3.4.1", "3.5.0")]
    [InlineData(null, "3.5.0")]
    [InlineData("3.4.1", "3.6.0")]
    [InlineData("3.4.1", "3.5.0-beta.3")]
    [InlineData("2.9.0", "v3.5.0")]
    public async Task Adopt_Crossing350_AddsTheDocsNoticeOnce(string? recorded, string adopted)
    {
        await InitPre350Project(recorded);

        var result = await AdoptAsync(adopted);

        result.AssertSuccess();
        Assert.Contains($"Added new-file nudge: {DocsMessage}", result.Stdout);
        var nudge = Assert.Single(DocsNudges());
        Assert.Equal("new-file", nudge["match"]!.GetValue<string>());
        Assert.Equal("notice", nudge["severity"]!.GetValue<string>());
        Assert.Equal(DocsMessage, nudge["message"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("3.4.1", "3.4.2")]
    [InlineData("3.5.0", "3.6.0")]
    [InlineData("3.5.0-beta.3", "3.5.0")]
    public async Task Adopt_NotCrossing350_AddsNothing(string recorded, string adopted)
    {
        await InitPre350Project(recorded);

        var result = await AdoptAsync(adopted);

        result.AssertSuccess();
        Assert.DoesNotContain("Added", result.Stdout);
        Assert.Empty(DocsNudges());
    }

    [Fact]
    public async Task Adopt_AfterTheUserRemovedTheNotice_DoesNotReAddIt()
    {
        await InitPre350Project("3.4.1");
        (await AdoptAsync("3.5.0")).AssertSuccess();
        RemoveDocsNudges();

        (await AdoptAsync("3.5.1")).AssertSuccess();

        Assert.Empty(DocsNudges());
    }

    [Fact]
    public async Task Adopt_KeepsAnEditedMessage_AndAddsNoDuplicate()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        SetDydoVersion("3.4.1");
        EditDocsNudgeMessage("Our docs go in dydo/.");

        var result = await AdoptAsync("3.5.0");

        result.AssertSuccess();
        var nudge = Assert.Single(DocsNudges());
        Assert.Equal("Our docs go in dydo/.", nudge["message"]!.GetValue<string>());
    }

    [Fact]
    public async Task Adopt_ProjectAlreadyHoldingTheNotice_AddsNoDuplicate()
    {
        (await InitProjectAsync("none")).AssertSuccess();
        SetDydoVersion("3.4.1");

        var result = await AdoptAsync("3.5.0");

        result.AssertSuccess();
        Assert.DoesNotContain("Added", result.Stdout);
        Assert.Single(DocsNudges());
    }

    [Theory]
    [InlineData("3.5.0-beta.3", "3.5.0", 0)]
    [InlineData("3.5.0", "3.5.0-beta.3", 0)]
    [InlineData("3.4.9", "3.5.0", -1)]
    [InlineData("3.10.0", "3.5.0", 1)]
    [InlineData("v3.5.0", "3.5.0", 0)]
    [InlineData("garbage", "0.0.1", -1)]
    public void CompareCore_PrereleaseCountsAsItsRelease(string a, string b, int expected)
    {
        Assert.Equal(expected, Math.Sign(AdoptCommand.CompareCore(a, b)));
    }

    /// <summary>An initialized project as dydo below 3.5.0 left it: no docs notice.</summary>
    private async Task InitPre350Project(string? recorded)
    {
        (await InitProjectAsync("none")).AssertSuccess();
        RemoveDocsNudges();
        SetDydoVersion(recorded);
    }

    private static bool IsDocsNudge(JsonNode? nudge) =>
        nudge?["pattern"]?.GetValue<string>() == ConfigFactory.DocsOutsideDydoNudge.Pattern;

    private List<JsonNode> DocsNudges() =>
        ReadConfigJson()["nudges"]!.AsArray().Where(IsDocsNudge).Select(n => n!).ToList();

    private void RemoveDocsNudges()
    {
        var raw = ReadConfigJson();
        var nudges = raw["nudges"]!.AsArray();
        foreach (var nudge in nudges.Where(IsDocsNudge).ToList())
            nudges.Remove(nudge);
        File.WriteAllText(ConfigPath, raw.ToJsonString());
    }

    private void EditDocsNudgeMessage(string message)
    {
        var raw = ReadConfigJson();
        raw["nudges"]!.AsArray().Single(IsDocsNudge)!["message"] = message;
        File.WriteAllText(ConfigPath, raw.ToJsonString());
    }

    #endregion

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
