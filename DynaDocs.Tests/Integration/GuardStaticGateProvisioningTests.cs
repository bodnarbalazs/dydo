namespace DynaDocs.Tests.Integration;

using System.Text.Json;

/// <summary>
/// DYD-539: an agent provisions the static-gate Python environment without user action. The
/// documented route writes only the repo-root, gitignored environment, while every write into
/// dydo/_system/** stays refused.
/// </summary>
[Collection("Integration")]
public sealed class GuardStaticGateProvisioningTests : IntegrationTestBase
{
    private const string StaticGateEnvironment = ".static-gates/python";
    private const string PreparedEnvironment = "/opt/prepared-checkout/.static-gates/python";
    private const string InstallNudge = "Installing or adding a library or tool needs the user's permission";

    public static TheoryData<string> DocumentedProvisioning => new()
    {
        $"python3.14 -m venv {StaticGateEnvironment}",
        $"python -m venv {StaticGateEnvironment}",
        $"mkdir -p .static-gates && ln -s {PreparedEnvironment} {StaticGateEnvironment}",
        $"{StaticGateEnvironment}/bin/python DynaDocs.Tests/coverage/gap_check.py gate static",
        $"{StaticGateEnvironment}/bin/python DynaDocs.Tests/coverage/gap_check.py gate coverage",
    };

    public static TheoryData<string> SystemWrites => new()
    {
        $"ln -s {PreparedEnvironment} dydo/_system/.local/static-gates/python",
        "touch dydo/_system/.local/static-gates/python/bin/python",
        $"cp -r {PreparedEnvironment} dydo/_system/.local/static-gates/python",
        "touch dydo/_system/roles/injected.json",
    };

    [Theory]
    [MemberData(nameof(DocumentedProvisioning))]
    public async Task Guard_AllowsDocumentedStaticGateProvisioning(string command)
    {
        await InitProjectAsync();

        var result = await GuardWithStdinAsync(Hook(command));

        result.AssertPreToolUseAllowed();
    }

    // The locked install is a warn nudge, not a hard block: it asks once, and the approved retry passes.
    [Fact]
    public async Task Guard_PassesTheApprovedRetryOfTheLockedInstall()
    {
        await InitProjectAsync();
        var install = $"{StaticGateEnvironment}/bin/python -m pip install -r DynaDocs.Tests/coverage/requirements.lock";

        var first = await GuardWithStdinAsync(Hook(install));
        var retry = await GuardWithStdinAsync(Hook(install));

        Assert.Contains(InstallNudge, first.AssertPreToolUseDenied());
        retry.AssertPreToolUseAllowed();
    }

    [Theory]
    [MemberData(nameof(SystemWrites))]
    public async Task Guard_StillRefusesWritesIntoDydoSystem(string command)
    {
        await InitProjectAsync();

        var first = await GuardWithStdinAsync(Hook(command));
        var retry = await GuardWithStdinAsync(Hook(command));

        first.AssertPreToolUseDenied();
        retry.AssertPreToolUseDenied();
    }

    private static string Hook(string command) => JsonSerializer.Serialize(new Dictionary<string, object>
    {
        ["session_id"] = TestSessionId,
        ["hook_event_name"] = "PreToolUse",
        ["tool_name"] = "Bash",
        ["tool_input"] = new { command },
    });
}
