namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Services;
using DynaDocs.Sync.Notion;

/// <summary>Connect stores the token in the local secret store and records the storage policy (and an
/// optional parent page) in dydo.json — without ever echoing the token. Each test runs against a temp
/// project root; the token store lands under that temp dydo, never the machine's real one.</summary>
[Collection("ConsoleOutput")]
public class NotionConnectServiceTests
{
    [Fact]
    public void Connect_StoresSecret_SetsTokenStorageLocal_WritesParentPage()
    {
        InTempProject((config, root) =>
        {
            var (code, stdout, _) = ConsoleCapture.All(() => NotionConnectService.Execute(
                config, () => "tok-abc", () => true, "page-123", vault: false, () => null, Console.Out, Console.Error));

            Assert.Equal(0, code);
            Assert.Equal("tok-abc", NotionTokenStore.Read(NotionTokenStore.PathFor(config.GetDydoRoot())));

            var reloaded = config.LoadConfig()!;
            Assert.Equal("local", reloaded.Notion!.TokenStorage);
            Assert.Equal("page-123", reloaded.Notion.ParentPageId);

            Assert.DoesNotContain("tok-abc", stdout);
        });
    }

    [Fact]
    public void Connect_ExistingToken_DeclineOverwrite_KeepsOriginal()
    {
        InTempProject((config, root) =>
        {
            var secretPath = NotionTokenStore.PathFor(config.GetDydoRoot());
            NotionTokenStore.Write(secretPath, "original");

            var code = ConsoleCapture.All(() => NotionConnectService.Execute(
                config, () => "replacement", () => false, null, vault: false, () => null, Console.Out, Console.Error)).exitCode;

            Assert.Equal(0, code);
            Assert.Equal("original", NotionTokenStore.Read(secretPath));
        });
    }

    [Fact]
    public void Connect_NoProject_ReportsCleanly()
    {
        var temp = Path.Combine(Path.GetTempPath(), "dydo-connect-noproj-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(temp);
        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(temp);
            var (code, _, stderr) = ConsoleCapture.All(() => NotionConnectService.Execute(
                new ConfigService(), () => "tok", () => true, null, vault: false, () => null, Console.Out, Console.Error));

            Assert.Equal(2, code);
            Assert.Contains("no dydo.json", stderr);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
            try { Directory.Delete(temp, true); } catch { }
        }
    }

    [Fact]
    public void Connect_Vault_SealsEncryptedVault_SetsVaultMode_Decryptable()
    {
        InTempProject((config, root) =>
        {
            const string passphrase = "Str0ng-Vault-Passphrase";

            var (code, stdout, _) = ConsoleCapture.All(() => NotionConnectService.Execute(
                config, () => "tok-vault", () => true, "pg-7", vault: true, () => passphrase, Console.Out, Console.Error));

            Assert.Equal(0, code);
            Assert.DoesNotContain("tok-vault", stdout);

            var reloaded = config.LoadConfig()!;
            Assert.Equal("vault", reloaded.Notion!.TokenStorage);
            Assert.Equal("pg-7", reloaded.Notion.ParentPageId);

            var envelope = NotionVault.ReadVault(NotionTokenStore.VaultPathFor(config.GetDydoRoot()));
            Assert.NotNull(envelope);
            Assert.Equal("tok-vault", NotionVault.Decrypt(envelope!, passphrase));
        });
    }

    [Fact]
    public void Connect_Vault_PassphraseMismatch_Rejected_NoVaultWritten()
    {
        InTempProject((config, root) =>
        {
            var reads = new Queue<string?>(["Str0ng-Passphrase-1", "Str0ng-Passphrase-2"]);

            var (code, _, stderr) = ConsoleCapture.All(() => NotionConnectService.Execute(
                config, () => "tok", () => true, null, vault: true, reads.Dequeue, Console.Out, Console.Error));

            Assert.Equal(2, code);
            Assert.Contains("did not match", stderr);
            Assert.False(File.Exists(NotionTokenStore.VaultPathFor(config.GetDydoRoot())));
        });
    }

    [Fact]
    public void Connect_Vault_NoPassphrase_Rejected()
    {
        InTempProject((config, root) =>
        {
            var (code, _, stderr) = ConsoleCapture.All(() => NotionConnectService.Execute(
                config, () => "tok", () => true, null, vault: true, () => "", Console.Out, Console.Error));

            Assert.Equal(2, code);
            Assert.Contains("no passphrase", stderr);
        });
    }

    [Fact]
    public void Connect_Vault_WeakPassphrase_Rejected()
    {
        InTempProject((config, root) =>
        {
            var (code, _, stderr) = ConsoleCapture.All(() => NotionConnectService.Execute(
                config, () => "tok", () => true, null, vault: true, () => "weakweak", Console.Out, Console.Error));

            Assert.Equal(2, code);
            Assert.Contains("too weak", stderr);
        });
    }

    [Fact]
    public void Connect_Vault_ExistingVault_DeclineOverwrite_KeepsOriginal()
    {
        InTempProject((config, root) =>
        {
            NotionVault.WriteVault(
                NotionTokenStore.VaultPathFor(config.GetDydoRoot()),
                NotionVault.Encrypt("original", "Str0ng-Original-Key", memoryKib: 8192, passes: 1));

            var code = ConsoleCapture.All(() => NotionConnectService.Execute(
                config, () => "replacement", () => false, null, vault: true, () => "Str0ng-New-Key", Console.Out, Console.Error)).exitCode;

            Assert.Equal(0, code);
            var envelope = NotionVault.ReadVault(NotionTokenStore.VaultPathFor(config.GetDydoRoot()));
            Assert.Equal("original", NotionVault.Decrypt(envelope!, "Str0ng-Original-Key"));
        });
    }

    private static void InTempProject(Action<ConfigService, string> body)
    {
        var root = Path.Combine(Path.GetTempPath(), "dydo-connect-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "dydo.json"), "{\"version\":1}");
        var savedCwd = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(root);
            body(new ConfigService(), root);
        }
        finally
        {
            Directory.SetCurrentDirectory(savedCwd);
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
