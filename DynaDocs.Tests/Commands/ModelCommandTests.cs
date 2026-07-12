namespace DynaDocs.Tests.Commands;

using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Serialization;

[Collection("ConsoleOutput")]
public class ModelCommandTests
{
    [Fact]
    public void Status_ActiveCap_PrintsModelFallbackAndResetTime()
    {
        InTempProject(root =>
        {
            var until = DateTimeOffset.Now.AddHours(2);
            WriteCap(root, "claude-fable-5", "claude-sonnet-5", until);

            var (code, output, _) = ConsoleCapture.All(() => ModelCommand.Create().Parse("status").Invoke());

            Assert.Equal(0, code);
            Assert.Contains("ACTIVE", output);
            Assert.Contains("claude-fable-5", output);
            Assert.Contains("claude-sonnet-5", output);
            Assert.Contains($"until {until:yyyy-MM-dd HH:mm}", output);
        });
    }

    [Fact]
    public void Status_NoCap_PrintsNoActiveCap()
    {
        InTempProject(_ =>
        {
            var (code, output, _) = ConsoleCapture.All(() => ModelCommand.Create().Parse("status").Invoke());

            Assert.Equal(0, code);
            Assert.Contains("No active model caps", output);
        });
    }

    [Fact]
    public void Status_ExpiredCap_ReportsInactiveAndPendingRestoration()
    {
        InTempProject(root =>
        {
            WriteCap(root, "claude-fable-5", "claude-sonnet-5", DateTimeOffset.Now.AddMinutes(-5));

            var (code, output, _) = ConsoleCapture.All(() => ModelCommand.Create().Parse("status").Invoke());

            Assert.Equal(0, code);
            Assert.Contains("No active model caps", output);
            Assert.Contains("expired", output, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("claude-fable-5", output);
        });
    }

    private static void InTempProject(Action<string> body)
    {
        var root = Path.Combine(Path.GetTempPath(), "dydo-modelcmd-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "dydo.json"), "{\"version\":1}");
        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(root);
            body(root);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static void WriteCap(string root, string model, string fallback, DateTimeOffset until)
    {
        var directory = Path.Combine(root, "dydo", "_system", ".local", "model-caps");
        Directory.CreateDirectory(directory);
        var cap = new ModelCap { Model = model, Fallback = fallback, Until = until };
        File.WriteAllText(Path.Combine(directory, model + ".json"),
            JsonSerializer.Serialize(cap, DydoDefaultJsonContext.Default.ModelCap));
    }
}
