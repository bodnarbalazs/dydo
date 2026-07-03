namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Services;
using DynaDocs.Sync.Notion;

/// <summary>Reveal is a guarded break-glass: with <c>--yes</c> (or an interactive yes) it prints the stored
/// token; otherwise it must not. Runs against a temp project whose secret store is seeded locally.</summary>
[Collection("ConsoleOutput")]
public class NotionRevealServiceTests
{
    [Fact]
    public void Reveal_WithYes_PrintsToken()
    {
        InSeededProject("tok-reveal", config =>
        {
            var (code, stdout, _) = ConsoleCapture.All(() => NotionRevealService.Execute(
                config, yes: true, () => false, Console.Out, Console.Error));

            Assert.Equal(0, code);
            Assert.Contains("tok-reveal", stdout);
        });
    }

    [Fact]
    public void Reveal_WithoutYes_DeclinedConfirm_DoesNotPrintToken()
    {
        InSeededProject("tok-secret", config =>
        {
            var (code, stdout, _) = ConsoleCapture.All(() => NotionRevealService.Execute(
                config, yes: false, () => false, Console.Out, Console.Error));

            Assert.Equal(0, code);
            Assert.DoesNotContain("tok-secret", stdout);
        });
    }

    [Fact]
    public void Reveal_NoStoredToken_ReportsCleanly()
    {
        InSeededProject(null, config =>
        {
            var (code, stdout, stderr) = ConsoleCapture.All(() => NotionRevealService.Execute(
                config, yes: true, () => false, Console.Out, Console.Error));

            Assert.Equal(2, code);
            Assert.Equal(string.Empty, stdout);
            Assert.Contains("no token stored", stderr);
        });
    }

    private static void InSeededProject(string? token, Action<ConfigService> body)
    {
        var root = Path.Combine(Path.GetTempPath(), "dydo-reveal-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "dydo.json"), "{\"version\":1}");
        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(root);
            var config = new ConfigService();
            if (token != null)
                NotionTokenStore.Write(NotionTokenStore.PathFor(config.GetDydoRoot()), token);
            body(config);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
