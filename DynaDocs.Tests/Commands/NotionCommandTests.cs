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
    public void SyncCommand_HasDocsOptOutFlags()
    {
        var sync = NotionCommand.Create().Subcommands.First(c => c.Name == "sync");
        Assert.Contains(sync.Options, o => o.Name == "--docs");
        Assert.Contains(sync.Options, o => o.Name == "--docs-only");
        Assert.Contains(sync.Options, o => o.Name == "--spine-only");
    }

    [Fact]
    public void Sync_DocsOnlyAndSpineOnly_RejectedBeforeAnyWork()
    {
        // The mutual-exclusion guard is the first thing RunSync checks, so it trips regardless of token/project.
        var (code, _, stderr) = ConsoleCapture.All(() =>
            NotionCommand.Create().Parse("sync --docs-only --spine-only").Invoke());

        Assert.Equal(1, code);
        Assert.Contains("mutually exclusive", stderr);
    }

    [Fact]
    public void Sync_DocsAndSpineOnly_RejectedBeforeAnyWork()
    {
        // Issue 0221 finding 3: --docs + --spine-only is contradictory and rejected, consistent with
        // --docs-only + --spine-only (before the fix --spine-only silently won and --docs was dropped).
        var (code, _, stderr) = ConsoleCapture.All(() =>
            NotionCommand.Create().Parse("sync --docs --spine-only").Invoke());

        Assert.Equal(1, code);
        Assert.Contains("mutually exclusive", stderr);
    }

    [Fact]
    public void Create_HasResetSubcommand()
    {
        var cmd = NotionCommand.Create();
        Assert.Contains(cmd.Subcommands, c => c.Name == "reset");
    }

    [Fact]
    public void ResetCommand_HasExpectedOptions()
    {
        var reset = NotionCommand.Create().Subcommands.First(c => c.Name == "reset");
        Assert.Contains(reset.Options, o => o.Name == "--dry-run");
        Assert.Contains(reset.Options, o => o.Name == "--yes");
        Assert.Contains(reset.Options, o => o.Name == "--parent-page");
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
    public void Sync_VaultMode_NoVaultFile_ReportsNotConfigured()
    {
        InTempProject(root =>
        {
            File.WriteAllText(
                Path.Combine(root, "dydo.json"),
                "{\"version\":1,\"notion\":{\"tokenStorage\":\"vault\"}}");

            // No notion.vault present, so the resolver returns null without ever prompting -> clean no-op.
            var (code, _, stderr) = ConsoleCapture.All(() => NotionCommand.Create().Parse("sync").Invoke());

            Assert.Equal(0, code);
            Assert.Contains("not configured", stderr);
        });
    }

    [Fact]
    public void Connect_Vault_SealsEncryptedVault_SetsVaultMode_NoPlaintextOnDisk()
    {
        InTempProject(root =>
        {
            const string token = "ntn_vault_secret_123";
            const string passphrase = "Sup3r-Vault-Key";

            var (code, stdout, _) = WithStdin($"{token}\n{passphrase}\n{passphrase}\n", () =>
                ConsoleCapture.All(() => NotionCommand.Create().Parse("connect --vault").Invoke()));

            Assert.Equal(0, code);
            Assert.DoesNotContain(token, stdout);

            var config = new ConfigService();
            Assert.Equal("vault", config.LoadConfig()!.Notion!.TokenStorage);

            var vaultPath = NotionTokenStore.VaultPathFor(config.GetDydoRoot());
            Assert.True(File.Exists(vaultPath));
            var onDisk = File.ReadAllText(vaultPath);
            Assert.DoesNotContain(token, onDisk);
            // The plaintext local secret must not exist in vault mode.
            Assert.False(NotionTokenStore.Exists(NotionTokenStore.PathFor(config.GetDydoRoot())));
        });
    }

    [Fact]
    public void Connect_Vault_WeakPassphrase_Rejected_NoVaultWritten()
    {
        InTempProject(root =>
        {
            var (code, _, stderr) = WithStdin("ntn_tok\nshort\nshort\n", () =>
                ConsoleCapture.All(() => NotionCommand.Create().Parse("connect --vault").Invoke()));

            Assert.Equal(2, code);
            Assert.Contains("too weak", stderr);

            var config = new ConfigService();
            Assert.False(File.Exists(NotionTokenStore.VaultPathFor(config.GetDydoRoot())));
        });
    }

    [Fact]
    public void Sync_VaultMode_VaultPresent_CannotUnlock_FailsClosed()
    {
        InTempProject(root =>
        {
            File.WriteAllText(
                Path.Combine(root, "dydo.json"),
                "{\"version\":1,\"notion\":{\"tokenStorage\":\"vault\"}}");
            var config = new ConfigService();
            NotionVault.WriteVault(
                NotionTokenStore.VaultPathFor(config.GetDydoRoot()),
                NotionVault.Encrypt("tok", "Sup3r-Vault-Key", 8192, 1));

            // A committed vault exists but no passphrase is supplied (empty stdin) -> resolver returns null.
            // Vault mode must fail CLOSED (exit 2), not silently no-op like the missing-vault case.
            var (code, _, stderr) = WithStdin("\n", () =>
                ConsoleCapture.All(() => NotionCommand.Create().Parse("sync").Invoke()));

            Assert.Equal(2, code);
            Assert.Contains("could not unlock the vault", stderr);
        });
    }

    [Theory]
    [InlineData("y", true)]
    [InlineData("Y", true)]
    [InlineData(" y ", true)]
    [InlineData("n", false)]
    [InlineData("yes", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsAffirmative_OnlyExactYAffirms_NullDeclines(string? line, bool expected) =>
        Assert.Equal(expected, NotionCommand.IsAffirmative(line));

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
