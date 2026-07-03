namespace DynaDocs.Tests.Commands;

using DynaDocs.Commands;
using DynaDocs.Services;
using DynaDocs.Sync.Notion;

[Collection("ConsoleOutput")]
public class NotionCommandTests
{
    [Fact]
    public void Sync_NoProcessToken_ExitsCleanly_WithoutNetworkCall()
    {
        // Clear the process token AND run from a dir with no dydo.json above it, so neither a token
        // nor a project resolves. On a clean env this hits the "not configured" gate; on a machine
        // where DYDO_NOTION_TOKEN is set in the Windows User registry the resolver still finds it,
        // so we tolerate either graceful message — both exit 0 and make no network call.
        var saved = Environment.GetEnvironmentVariable(NotionCommand.TokenEnvVar);
        var savedCwd = Directory.GetCurrentDirectory();
        var temp = Path.Combine(Path.GetTempPath(), "dydo-notion-notoken-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable(NotionCommand.TokenEnvVar, null);
        try
        {
            Directory.SetCurrentDirectory(temp);
            var (code, _, stderr) = ConsoleCapture.All(() => NotionCommand.Create().Parse("sync").Invoke());

            Assert.Equal(0, code);
            Assert.True(stderr.Contains("not configured") || stderr.Contains("no dydo.json"), stderr);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
            Environment.SetEnvironmentVariable(NotionCommand.TokenEnvVar, saved);
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    [Fact]
    public void Sync_WithToken_NoProject_ReportsNoConfig()
    {
        // With a token present but no dydo.json on the path, the command reports cleanly and makes
        // no network call. Run from a temp dir with no dydo.json above it is impractical (the repo
        // root has one), so we drive the no-project branch by running from the system temp root.
        var saved = Environment.GetEnvironmentVariable(NotionCommand.TokenEnvVar);
        var savedCwd = Directory.GetCurrentDirectory();
        var temp = Path.Combine(Path.GetTempPath(), "dydo-notion-noproj-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(temp);
        Environment.SetEnvironmentVariable(NotionCommand.TokenEnvVar, "secret-token");
        try
        {
            Directory.SetCurrentDirectory(temp);
            var (code, _, stderr) = ConsoleCapture.All(() => NotionCommand.Create().Parse("sync").Invoke());

            Assert.Equal(0, code);
            Assert.Contains("no dydo.json", stderr);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
            Environment.SetEnvironmentVariable(NotionCommand.TokenEnvVar, saved);
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    [Fact]
    public void Create_HasSyncSubcommand()
    {
        var cmd = NotionCommand.Create();
        Assert.Contains(cmd.Subcommands, c => c.Name == "sync");
    }

    [Fact]
    public void SyncCommand_HasDryRunOption()
    {
        var sync = NotionCommand.Create().Subcommands.First(c => c.Name == "sync");
        Assert.Contains(sync.Options, o => o.Name == "--dry-run");
    }

    [Fact]
    public void Create_HasConnectAndRevealSubcommands()
    {
        var cmd = NotionCommand.Create();
        Assert.Contains(cmd.Subcommands, c => c.Name == "connect");
        Assert.Contains(cmd.Subcommands, c => c.Name == "reveal-token");
    }

    [Fact]
    public void Connect_Command_StoresToken_FromStdin_WithoutEchoing()
    {
        InTempProject(root =>
        {
            var (code, stdout, _) = WithStdin("cmd-tok\n", () =>
                ConsoleCapture.All(() => NotionCommand.Create().Parse("connect --parent-page pg-9").Invoke()));

            Assert.Equal(0, code);
            Assert.DoesNotContain("cmd-tok", stdout);

            var config = new ConfigService();
            Assert.Equal("cmd-tok", NotionTokenStore.Read(NotionTokenStore.PathFor(config.GetDydoRoot())));
            var reloaded = config.LoadConfig()!;
            Assert.Equal("local", reloaded.Notion!.TokenStorage);
            Assert.Equal("pg-9", reloaded.Notion.ParentPageId);
        });
    }

    [Fact]
    public void Connect_Command_ExistingToken_ConfirmYes_Overwrites()
    {
        InTempProject(root =>
        {
            var config = new ConfigService();
            NotionTokenStore.Write(NotionTokenStore.PathFor(config.GetDydoRoot()), "old-tok");

            var code = WithStdin("y\nnew-tok\n", () =>
                ConsoleCapture.All(() => NotionCommand.Create().Parse("connect").Invoke())).exitCode;

            Assert.Equal(0, code);
            Assert.Equal("new-tok", NotionTokenStore.Read(NotionTokenStore.PathFor(config.GetDydoRoot())));
        });
    }

    [Fact]
    public void RevealToken_Command_WithYes_PrintsToken()
    {
        InTempProject(root =>
        {
            var config = new ConfigService();
            NotionTokenStore.Write(NotionTokenStore.PathFor(config.GetDydoRoot()), "seed-tok");

            var (code, stdout, _) = ConsoleCapture.All(() =>
                NotionCommand.Create().Parse("reveal-token --yes").Invoke());

            Assert.Equal(0, code);
            Assert.Contains("seed-tok", stdout);
        });
    }

    [Fact]
    public void RevealToken_Command_ConfirmNo_DoesNotPrintToken()
    {
        InTempProject(root =>
        {
            var config = new ConfigService();
            NotionTokenStore.Write(NotionTokenStore.PathFor(config.GetDydoRoot()), "seed-tok");

            var (code, stdout, _) = WithStdin("n\n", () =>
                ConsoleCapture.All(() => NotionCommand.Create().Parse("reveal-token").Invoke()));

            Assert.Equal(0, code);
            Assert.DoesNotContain("seed-tok", stdout);
        });
    }

    [Fact]
    public void Sync_VaultMode_ReportsNotImplemented()
    {
        InTempProject(root =>
        {
            File.WriteAllText(
                Path.Combine(root, "dydo.json"),
                "{\"version\":1,\"notion\":{\"tokenStorage\":\"vault\"}}");

            var (code, _, stderr) = ConsoleCapture.All(() => NotionCommand.Create().Parse("sync").Invoke());

            Assert.Equal(2, code);
            Assert.Contains("vault", stderr);
        });
    }

    private static void InTempProject(Action<string> body)
    {
        var root = Path.Combine(Path.GetTempPath(), "dydo-notioncmd-" + Guid.NewGuid().ToString("N")[..8]);
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

    private static T WithStdin<T>(string input, Func<T> body)
    {
        var savedIn = Console.In;
        Console.SetIn(new StringReader(input));
        try { return body(); }
        finally { Console.SetIn(savedIn); }
    }
}
