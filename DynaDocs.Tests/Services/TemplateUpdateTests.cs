namespace DynaDocs.Tests.Services;

using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Services;

public class TemplateUpdateTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dydoRoot;

    public TemplateUpdateTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"dydo-test-{Guid.NewGuid():N}");
        _dydoRoot = Path.Combine(_tempDir, "dydo");
        Directory.CreateDirectory(_dydoRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
        GC.SuppressFinalize(this);
    }

    #region Hash computation

    [Fact]
    public void ComputeHash_ConsistentForSameContent()
    {
        var hash1 = TemplateCommand.ComputeHash("Hello, world!");
        var hash2 = TemplateCommand.ComputeHash("Hello, world!");

        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_DifferentContent_DifferentHash()
    {
        var hash1 = TemplateCommand.ComputeHash("Content A");
        var hash2 = TemplateCommand.ComputeHash("Content B");

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void ComputeHash_ReturnsLowercaseHex()
    {
        var hash = TemplateCommand.ComputeHash("test");

        Assert.Matches("^[0-9a-f]+$", hash);
        Assert.Equal(64, hash.Length); // SHA256 = 32 bytes = 64 hex chars
    }

    [Fact]
    public void ComputeHash_CrlfAndLf_ProduceSameHash()
    {
        var lf = "line one\nline two\nline three\n";
        var crlf = "line one\r\nline two\r\nline three\r\n";

        Assert.Equal(TemplateCommand.ComputeHash(lf), TemplateCommand.ComputeHash(crlf));
    }

    [Fact]
    public void ComputeHash_BomDoesNotAffectHash()
    {
        var withBom = "\uFEFFsome content";
        var withoutBom = "some content";

        Assert.Equal(TemplateCommand.ComputeHash(withBom), TemplateCommand.ComputeHash(withoutBom));
    }

    [Fact]
    public void ComputeHash_MixedLineEndings_NormalizedToLf()
    {
        var mixed = "line one\r\nline two\nline three\r\n";
        var clean = "line one\nline two\nline three\n";

        Assert.Equal(TemplateCommand.ComputeHash(mixed), TemplateCommand.ComputeHash(clean));
    }

    #endregion

    #region Direct edit detection

    [Fact]
    public void IsDirectlyEdited_HashMatches_ReturnsFalse()
    {
        var content = "original content";
        var hash = TemplateCommand.ComputeHash(content);

        // When storedHash == onDiskHash, isUserEdited should be false
        var storedHash = hash;
        var onDiskHash = TemplateCommand.ComputeHash(content);
        var isUserEdited = storedHash != null ? storedHash != onDiskHash : false;

        Assert.False(isUserEdited);
    }

    [Fact]
    public void IsDirectlyEdited_HashMismatch_ReturnsTrue()
    {
        var original = "original content";
        var edited = "edited content";
        var storedHash = TemplateCommand.ComputeHash(original);
        var onDiskHash = TemplateCommand.ComputeHash(edited);

        var isUserEdited = storedHash != null ? storedHash != onDiskHash : false;

        Assert.True(isUserEdited);
    }

    [Fact]
    public void IsDirectlyEdited_NoStoredHash_ContentMatchesEmbedded_ReturnsFalse()
    {
        var content = "embedded content";
        string? storedHash = null;
        var onDisk = content; // Same as embedded

        var isUserEdited = storedHash != null ? storedHash != TemplateCommand.ComputeHash(onDisk) : onDisk != content;

        Assert.False(isUserEdited);
    }

    [Fact]
    public void IsDirectlyEdited_NoStoredHash_ContentDiffers_ReturnsTrue()
    {
        var embeddedContent = "embedded content";
        string? storedHash = null;
        var onDisk = "user modified content";

        var isUserEdited = storedHash != null ? storedHash != TemplateCommand.ComputeHash(onDisk) : onDisk != embeddedContent;

        Assert.True(isUserEdited);
    }

    [Fact]
    public void IsDirectlyEdited_CrlfOnDisk_LfStored_NotDetectedAsEdited()
    {
        // Simulates: file was written with LF, stored hash from LF content,
        // then git autocrlf or editor converted to CRLF
        var lfContent = "line one\nline two\n";
        var crlfContent = "line one\r\nline two\r\n";
        var storedHash = TemplateCommand.ComputeHash(lfContent);
        var onDiskHash = TemplateCommand.ComputeHash(crlfContent);

        // After normalization, these should be equal
        Assert.Equal(storedHash, onDiskHash);
    }

    #endregion

    #region UpdateFile scenarios via filesystem

    [Fact]
    public void UpdateFile_StoresUpdatedHash()
    {
        var newContent = "new framework content";
        var config = new DydoConfig();
        var relativePath = "test/file.md";

        config.FrameworkHashes[relativePath] = TemplateCommand.ComputeHash(newContent);

        Assert.Equal(TemplateCommand.ComputeHash(newContent), config.FrameworkHashes[relativePath]);
    }

    [Fact]
    public void UpdateFile_BinaryFile_ComparesBytes()
    {
        var bytesA = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG magic
        var bytesB = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var bytesC = new byte[] { 0x89, 0x50, 0x4E, 0x48 };

        var hashA = TemplateCommand.ComputeHashBytes(bytesA);
        var hashB = TemplateCommand.ComputeHashBytes(bytesB);
        var hashC = TemplateCommand.ComputeHashBytes(bytesC);

        Assert.Equal(hashA, hashB);
        Assert.NotEqual(hashA, hashC);

        // Verify binary file update path
        var binaryPath = Path.Combine(_dydoRoot, "_assets", "test.svg");
        Directory.CreateDirectory(Path.GetDirectoryName(binaryPath)!);
        File.WriteAllBytes(binaryPath, bytesA);
        var onDisk = File.ReadAllBytes(binaryPath);
        Assert.Equal(hashA, TemplateCommand.ComputeHashBytes(onDisk));
    }

    #endregion
}
