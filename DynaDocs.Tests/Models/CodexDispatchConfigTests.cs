namespace DynaDocs.Tests.Models;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;

public class CodexDispatchConfigTests
{
    [Fact]
    public void Defaults_AreShippedPosture()
    {
        var codex = new CodexDispatchConfig();

        Assert.Equal(ConfigFactory.DefaultCodexSandbox, codex.Sandbox);
        Assert.Equal(ConfigFactory.DefaultCodexApprovalPolicy, codex.ApprovalPolicy);
        Assert.Equal("workspace-write", codex.Sandbox);
        Assert.Equal("on-request", codex.ApprovalPolicy);
    }

    [Fact]
    public void Validate_Defaults_ReturnsNoErrors()
    {
        Assert.Empty(new CodexDispatchConfig().Validate());
    }

    [Theory]
    [InlineData("read-only")]
    [InlineData("workspace-write")]
    [InlineData("danger-full-access")]
    public void Validate_AcceptedSandbox_ReturnsNoErrors(string sandbox)
    {
        Assert.Empty(new CodexDispatchConfig { Sandbox = sandbox }.Validate());
    }

    [Theory]
    [InlineData("untrusted")]
    [InlineData("on-request")]
    [InlineData("never")]
    public void Validate_AcceptedApprovalPolicy_ReturnsNoErrors(string policy)
    {
        Assert.Empty(new CodexDispatchConfig { ApprovalPolicy = policy }.Validate());
    }

    [Fact]
    public void Validate_InvalidSandbox_NamesAcceptedList()
    {
        var errors = new CodexDispatchConfig { Sandbox = "loose" }.Validate();

        var error = Assert.Single(errors);
        Assert.Contains("loose", error);
        Assert.Contains("read-only", error);
        Assert.Contains("workspace-write", error);
        Assert.Contains("danger-full-access", error);
    }

    [Fact]
    public void Validate_OnFailureApprovalPolicy_Rejected()
    {
        // on-failure is DEPRECATED in the codex CLI — not an accepted value.
        var errors = new CodexDispatchConfig { ApprovalPolicy = "on-failure" }.Validate();

        var error = Assert.Single(errors);
        Assert.Contains("on-failure", error);
        Assert.Contains("on-request", error);
    }

    [Fact]
    public void Validate_BothInvalid_ReturnsBothErrors()
    {
        var errors = new CodexDispatchConfig { Sandbox = "x", ApprovalPolicy = "y" }.Validate();

        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Deserialize_AbsentCodexSection_ResolvesToShippedDefaults()
    {
        const string json = """{ "launchInTab": true, "autoClose": false }""";

        var dispatch = JsonSerializer.Deserialize(json, DydoConfigJsonContext.Default.DispatchConfig);

        Assert.NotNull(dispatch);
        Assert.Equal("workspace-write", dispatch!.Codex.Sandbox);
        Assert.Equal("on-request", dispatch.Codex.ApprovalPolicy);
    }

    [Fact]
    public void Deserialize_PresentCodexSection_ReadsConfiguredValues()
    {
        const string json = """{ "codex": { "sandbox": "read-only", "approvalPolicy": "never" } }""";

        var dispatch = JsonSerializer.Deserialize(json, DydoConfigJsonContext.Default.DispatchConfig);

        Assert.NotNull(dispatch);
        Assert.Equal("read-only", dispatch!.Codex.Sandbox);
        Assert.Equal("never", dispatch.Codex.ApprovalPolicy);
    }
}
