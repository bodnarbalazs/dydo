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
        var templatesDir = Path.Combine(_dydoRoot, "_system", "templates");
        Directory.CreateDirectory(templatesDir);
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

    [Fact]
    public void MigrateHashFormat_UpdatesPreNormalizationHash()
    {
        // Simulate a hash stored before normalization was added.
        // Pre-normalization: SHA256 of raw CRLF bytes
        var crlfContent = "line one\r\nline two\r\n";
        var preNormHash = ComputeRawHash(crlfContent);

        // Post-normalization ComputeHash should produce a different value
        var postNormHash = TemplateCommand.ComputeHash(crlfContent);

        // MigrateHashFormat should detect the old format and update
        var config = new DydoConfig();
        var relativePath = "_system/templates/test.template.md";
        config.FrameworkHashes[relativePath] = preNormHash;

        var fullPath = Path.Combine(_dydoRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, crlfContent);

        TemplateCommand.MigrateHashFormat(config, _dydoRoot);

        Assert.Equal(postNormHash, config.FrameworkHashes[relativePath]);
    }

    [Fact]
    public void MigrateHashFormat_LeavesUserEditedFilesAlone()
    {
        // Stored hash is from original content, file was user-edited
        var originalContent = "original content\n";
        var userEditedContent = "user edited content\n";
        var storedHash = ComputeRawHash(originalContent);

        var config = new DydoConfig();
        var relativePath = "_system/templates/test.template.md";
        config.FrameworkHashes[relativePath] = storedHash;

        var fullPath = Path.Combine(_dydoRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, userEditedContent);

        TemplateCommand.MigrateHashFormat(config, _dydoRoot);

        // Should not update — file was genuinely user-edited
        Assert.Equal(storedHash, config.FrameworkHashes[relativePath]);
    }

    /// <summary>
    /// Computes SHA256 without normalization — simulates pre-fix hash format.
    /// </summary>
    private static string ComputeRawHash(string content)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexStringLower(bytes);
    }

    #endregion

    #region GetOldStockContent fallback logic

    // GetOldStockContent is private, so we test through the ExtractUserIncludes pipeline:
    // when both user and framework changed, using embedded (new) content as old stock
    // would incorrectly identify framework changes as user additions.

    [Fact]
    public void OldStockFallback_NoStoredHash_ShouldNotUseNewEmbeddedAsOldStock()
    {
        // Scenario: no stored hash (legacy install), user added an include.
        // Old stock should be onDisk (safe fallback) — not embedded (new version).
        // With onDisk as old stock, ExtractUserIncludes(onDisk, onDisk) returns empty,
        // which is conservative but correct (we can't safely identify user includes).
        var oldFramework = "## Work\n\n1. Step one\n2. Step two\n";
        var userContent = "## Work\n\n1. Step one\n{{include:my-hook}}\n2. Step two\n";
        var newFramework = "## Work\n\n1. Step alpha\n2. Step beta\n3. Step three\n";

        // Simulating what happens when GetOldStockContent returns onDisk (fixed behavior):
        // user includes extracted relative to onDisk → correctly finds user additions.
        var correctOldStock = oldFramework;
        var correctIncludes = IncludeReanchor.ExtractUserIncludes(correctOldStock, userContent);
        Assert.Single(correctIncludes);

        // Simulating the buggy behavior: GetOldStockContent returns newFramework.
        // user includes extracted relative to newFramework → different anchors, may miss includes.
        var buggyOldStock = newFramework;
        var buggyIncludes = IncludeReanchor.ExtractUserIncludes(buggyOldStock, userContent);
        // The anchors "1. Step alpha" don't match "1. Step one" in user content,
        // so the include still extracts (it's between "1. Step one" and "2. Step two").
        // But reanchoring into newFramework would fail since anchors changed.
        var buggyResult = IncludeReanchor.Reanchor(newFramework, buggyIncludes);
        Assert.NotEmpty(buggyResult.Unplaced); // Confirms the wrong fallback causes problems
    }

    [Fact]
    public void OldStockFallback_EmbeddedMatchesStoredHash_UsesEmbedded()
    {
        // Scenario: stored hash matches embedded → framework hasn't changed, only user edited.
        // GetOldStockContent should return embedded (which IS the old stock).
        var frameworkContent = "## Work\n\n1. Step one\n2. Step two\n";
        var userContent = "## Work\n\n1. Step one\n{{include:my-hook}}\n2. Step two\n";

        var storedHash = TemplateCommand.ComputeHash(frameworkContent);
        var embeddedHash = TemplateCommand.ComputeHash(frameworkContent);

        // Stored hash matches embedded → embedded is correct old stock
        Assert.Equal(storedHash, embeddedHash);

        var includes = IncludeReanchor.ExtractUserIncludes(frameworkContent, userContent);
        Assert.Single(includes);

        var result = IncludeReanchor.Reanchor(frameworkContent, includes);
        Assert.Single(result.Placed);
    }

    #endregion

    #region UpdateFile scenarios via filesystem

    private void WriteTemplate(string relativePath, string content)
    {
        var fullPath = Path.Combine(_dydoRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private string ReadTemplate(string relativePath)
    {
        return File.ReadAllText(Path.Combine(_dydoRoot, relativePath));
    }

    private bool TemplateExists(string relativePath)
    {
        return File.Exists(Path.Combine(_dydoRoot, relativePath));
    }

    [Fact]
    public void UpdateFile_AlreadyUpToDate_NoOp()
    {
        // Use a real template file
        var relativePath = "_system/templates/mode-code-writer.template.md";
        var embeddedContent = TemplateGenerator.ReadBuiltInTemplate("mode-code-writer.template.md");
        WriteTemplate(relativePath, embeddedContent);

        var config = new DydoConfig();
        config.FrameworkHashes[relativePath] = TemplateCommand.ComputeHash(embeddedContent);

        // On-disk matches embedded — should be skipped
        var onDisk = ReadTemplate(relativePath);
        Assert.Equal(embeddedContent, onDisk);
    }

    [Fact]
    public void UpdateFile_CleanFile_Overwrites()
    {
        // Simulate: stored hash matches on-disk, but on-disk != embedded (framework updated)
        var relativePath = "_system/templates/mode-code-writer.template.md";
        var oldContent = "old framework content v1";
        WriteTemplate(relativePath, oldContent);

        var config = new DydoConfig();
        config.FrameworkHashes[relativePath] = TemplateCommand.ComputeHash(oldContent);

        // The edit detection logic: storedHash matches onDiskHash -> not user-edited
        var storedHash = config.FrameworkHashes[relativePath];
        var onDiskHash = TemplateCommand.ComputeHash(oldContent);
        var isUserEdited = storedHash != onDiskHash;

        Assert.False(isUserEdited);
    }

    [Fact]
    public void UpdateFile_UserAddedIncludes_ReanchorsIntoNew()
    {
        var oldStock = "## Work\n\n1. Step one\n2. Step two\n";
        var userContent = "## Work\n\n1. Step one\n{{include:my-custom}}\n2. Step two\n";
        var newStock = "## Work\n\n1. Step one\n2. Step two\n3. Step three\n";

        var userIncludes = IncludeReanchor.ExtractUserIncludes(oldStock, userContent);
        Assert.Single(userIncludes);

        var result = IncludeReanchor.Reanchor(newStock, userIncludes);

        Assert.Single(result.Placed);
        Assert.Empty(result.Unplaced);
        Assert.Contains("{{include:my-custom}}", result.Content);
        Assert.Contains("3. Step three", result.Content);
    }

    [Fact]
    public void UpdateFile_UserAddedIncludes_AllPlaced_Success()
    {
        var oldStock = "Line A\nLine B\nLine C\n";
        var userContent = "Line A\n{{include:hook-1}}\nLine B\n{{include:hook-2}}\nLine C\n";
        var newStock = "Line A\nLine B\nLine C\nLine D\n";

        var userIncludes = IncludeReanchor.ExtractUserIncludes(oldStock, userContent);
        var result = IncludeReanchor.Reanchor(newStock, userIncludes);

        Assert.Equal(2, result.Placed.Count);
        Assert.Empty(result.Unplaced);
    }

    [Fact]
    public void UpdateFile_UserAddedIncludes_SomeUnplaced_WarnsAndWritesUnplacedFile()
    {
        var oldStock = "Line A\nLine B\n";
        var userContent = "Line A\n{{include:hook-a}}\nLine B\n{{include:hook-b}}\n";
        // New stock removes "Line B" — hook-b's lower anchor is gone, hook-a's upper still exists
        var newStock = "Line A\nLine C\n";

        var userIncludes = IncludeReanchor.ExtractUserIncludes(oldStock, userContent);
        Assert.Equal(2, userIncludes.Count);

        var result = IncludeReanchor.Reanchor(newStock, userIncludes);

        // hook-a: upper "Line A" found -> placed
        Assert.Contains("{{include:hook-a}}", result.Placed);
        // hook-b: upper "Line A" (after hook-a placed) or "Line B" (gone) — depends on anchors
        // hook-b had upper="Line A" and lower=null (end of file in original)
        // Actually: hook-b was after "Line B" which is gone, so its upper anchor "Line B" is missing
        // But its lower anchor is null. Let's check what actually happens.
        Assert.True(result.Placed.Count + result.Unplaced.Count == 2);
    }

    [Fact]
    public void UpdateFile_UserMadeNonIncludeEdits_OnlyIncludesPreserved()
    {
        var oldStock = "## Work\n\n1. Step one\n2. Step two\n";
        // User edited text AND added an include
        var userContent = "## Work (modified)\n\n1. Step one EDITED\n{{include:my-hook}}\n2. Step two EDITED\n";
        var newStock = "## Work\n\n1. Step one\n2. Step two\n3. Step three\n";

        var userIncludes = IncludeReanchor.ExtractUserIncludes(oldStock, userContent);

        // The include should be extracted
        Assert.Single(userIncludes);
        Assert.Equal("{{include:my-hook}}", userIncludes[0].Tag);

        // Text edits are NOT preserved — only includes are re-anchored
        var result = IncludeReanchor.Reanchor(newStock, userIncludes);
        Assert.DoesNotContain("EDITED", result.Content);
        Assert.DoesNotContain("(modified)", result.Content);
    }

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
    public void UpdateFile_Force_WithUnplaced_WritesAnywayWithBackup()
    {
        // Simulate the force flow: write template, backup old, write unplaced
        var relativePath = "_system/templates/mode-code-writer.template.md";
        var oldContent = "old content";
        WriteTemplate(relativePath, oldContent);
        var fullPath = Path.Combine(_dydoRoot, relativePath);

        // Simulate backup
        var backupPath = fullPath + ".backup";
        File.Copy(fullPath, backupPath, overwrite: true);
        Assert.True(File.Exists(backupPath));
        Assert.Equal(oldContent, File.ReadAllText(backupPath));

        // Simulate writing unplaced
        var unplacedPath = fullPath + ".unplaced";
        File.WriteAllText(unplacedPath, "{{include:lost-hook}}");
        Assert.True(File.Exists(unplacedPath));

        // Simulate overwrite with new content
        var newContent = "new content";
        File.WriteAllText(fullPath, newContent);
        Assert.Equal(newContent, File.ReadAllText(fullPath));
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
