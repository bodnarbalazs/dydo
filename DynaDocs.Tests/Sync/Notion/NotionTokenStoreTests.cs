namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync.Notion;

/// <summary>Local secret-store round-trip (Decision 027 §3). Every test writes to a temp path so the
/// machine's real <c>dydo/_system/.local</c> is never touched. On Windows the on-disk form is
/// DPAPI ciphertext (<c>dpapi:</c>+base64) and must not contain the plaintext; elsewhere it is
/// <c>plain:</c>+token at mode <c>0600</c>.</summary>
public class NotionTokenStoreTests
{
    private static string TempSecretPath() =>
        Path.Combine(Path.GetTempPath(), "dydo-store-" + Guid.NewGuid().ToString("N")[..8], NotionTokenStore.SecretFileName);

    [Fact]
    public void WriteThenRead_RoundTrips()
    {
        var path = TempSecretPath();
        try
        {
            NotionTokenStore.Write(path, "secret-XYZ-123");
            Assert.Equal("secret-XYZ-123", NotionTokenStore.Read(path));
            Assert.True(NotionTokenStore.Exists(path));
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void OnDisk_DoesNotStorePlaintext_OrIsPlain0600()
    {
        var path = TempSecretPath();
        try
        {
            const string token = "plaintext-should-not-leak";
            NotionTokenStore.Write(path, token);
            var text = File.ReadAllText(path);

            if (OperatingSystem.IsWindows())
            {
                Assert.StartsWith("dpapi:", text);
                // The whole file is ASCII (dpapi: + base64 of ciphertext); the raw token must not survive.
                Assert.DoesNotContain(token, text);
            }
            else
            {
                Assert.StartsWith("plain:", text);
                Assert.Equal("plain:" + token, text);
                var mode = File.GetUnixFileMode(path);
                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, mode);
            }
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Read_MissingFile_ReturnsNull()
    {
        var path = TempSecretPath();
        Assert.Null(NotionTokenStore.Read(path));
        Assert.False(NotionTokenStore.Exists(path));
    }

    [Fact]
    public void Read_CorruptOrUndecryptable_ReturnsNull_NeverThrows()
    {
        var path = TempSecretPath();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            // Valid base64 but not a real DPAPI blob -> unprotect fails on Windows; the dpapi marker on a
            // non-Windows host is likewise undecryptable. Either way: null, not an exception.
            File.WriteAllText(path, "dpapi:AAAAAAAA");
            Assert.Null(NotionTokenStore.Read(path));

            File.WriteAllText(path, "garbage-without-a-known-marker");
            Assert.Null(NotionTokenStore.Read(path));
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Delete_RemovesTheSecret()
    {
        var path = TempSecretPath();
        try
        {
            NotionTokenStore.Write(path, "tok");
            Assert.True(NotionTokenStore.Exists(path));
            NotionTokenStore.Delete(path);
            Assert.False(NotionTokenStore.Exists(path));
        }
        finally { Cleanup(path); }
    }

    [Fact]
    public void Write_DropsLocalGitignore_SoTheSecretCanNeverBeStaged()
    {
        var path = TempSecretPath();
        try
        {
            NotionTokenStore.Write(path, "tok");
            var ignorePath = Path.Combine(Path.GetDirectoryName(path)!, ".gitignore");
            Assert.True(File.Exists(ignorePath), "Write must drop a .gitignore next to the secret.");
            Assert.Contains("*", File.ReadAllText(ignorePath));
        }
        finally { Cleanup(path); }
    }

    private static void Cleanup(string path)
    {
        try { Directory.Delete(Path.GetDirectoryName(path)!, true); } catch { }
    }
}
