namespace DynaDocs.Tests.Integration;

using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Services;

/// <summary>Injected command-parser seam evidence, not a real host approval UI or public CLI isolation.</summary>
[Collection("Integration")]
public sealed class VerificationModeTests : IntegrationTestBase
{
    [Theory]
    [InlineData("off", "on")]
    [InlineData("on", "off")]
    public async Task DenialPreservesOwnedState_ThenExplicitParserTransitionNeedsReadback(string initial, string target)
    {
        var root = Path.GetFullPath(Path.Combine(TestDir, "machine-state"));
        Assert.StartsWith(Path.GetFullPath(TestDir) + Path.DirectorySeparatorChar, root);
        Assert.NotEqual(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dydo"), root);
        Assert.False(Directory.Exists(root));
        var state = new ModeState(root);
        (await RunAsync(ModeCommand.Create(state), "set", "overnight", initial)).AssertSuccess();
        var path = Path.Combine(root, "mode.json");
        Assert.True(File.Exists(path));
        var before = await File.ReadAllTextAsync(path);
        (await InitProjectAsync("none")).AssertSuccess();

        var payload = JsonSerializer.Serialize(new
        {
            session_id = TestSessionId,
            hook_event_name = "PreToolUse",
            tool_name = "shell_command",
            permission_mode = "default",
            tool_input = new { command = $"dydo mode set overnight {target}" }
        });
        var denied = await GuardWithStdinAsync(payload);
        Assert.Contains($"own terminal: dydo mode set overnight {target}", denied.AssertPreToolUseDenied());
        Assert.Equal(before, await File.ReadAllTextAsync(path));
        var unchanged = await RunAsync(ModeCommand.Create(new ModeState(root)), "show");
        unchanged.AssertSuccess();
        Assert.Equal($"overnight: {initial}", unchanged.Stdout.Trim());

        (await RunAsync(ModeCommand.Create(state), "set", "overnight", target)).AssertSuccess();
        var readback = await RunAsync(ModeCommand.Create(new ModeState(root)), "show");
        readback.AssertSuccess();
        Assert.Equal($"overnight: {target}", readback.Stdout.Trim());
        Assert.NotEqual(before, await File.ReadAllTextAsync(path));
    }
}
