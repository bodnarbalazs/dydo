namespace DynaDocs.Tests.Services;

using DynaDocs.Services;

public class OffLimitsServiceTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _dydoDir;

    public OffLimitsServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-offlimits-test-" + Guid.NewGuid().ToString("N")[..8]);
        _dydoDir = Path.Combine(_testDir, "dydo");
        Directory.CreateDirectory(_dydoDir);

        // Create minimal dydo.json
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            {
                "version": 1,
                "structure": { "root": "dydo" },
                "agents": { "pool": [], "assignments": {} }
            }
            """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    #region Loading Patterns

    [Fact]
    public void LoadPatterns_LoadsFromMarkdownCodeBlock()
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), """
            # Files Off-Limits

            ```
            .env
            secrets.json
            ```
            """);

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        Assert.Contains(".env", service.Patterns);
        Assert.Contains("secrets.json", service.Patterns);
        Assert.Equal(2, service.Patterns.Count);
    }

    [Fact]
    public void LoadPatterns_LoadsFromListItems()
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), """
            # Files Off-Limits

            - .env
            - secrets.json
            * api-keys.txt
            """);

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        Assert.Contains(".env", service.Patterns);
        Assert.Contains("secrets.json", service.Patterns);
        Assert.Contains("api-keys.txt", service.Patterns);
        Assert.Equal(3, service.Patterns.Count);
    }

    [Fact]
    public void LoadPatterns_IgnoresCommentLines()
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), """
            ```
            # This is a comment
            .env
            # Another comment
            secrets.json
            ```
            """);

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        Assert.Equal(2, service.Patterns.Count);
        Assert.Contains(".env", service.Patterns);
        Assert.Contains("secrets.json", service.Patterns);
    }

    [Fact]
    public void LoadPatterns_HandlesInlineComments()
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), """
            ```
            .env  # inline comment
            secrets.json #another
            ```
            """);

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        Assert.Equal(2, service.Patterns.Count);
        Assert.Contains(".env", service.Patterns);
        Assert.Contains("secrets.json", service.Patterns);
    }

    [Fact]
    public void LoadPatterns_IgnoresEmptyLines()
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), """
            ```
            .env

            secrets.json

            ```
            """);

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        Assert.Equal(2, service.Patterns.Count);
    }

    [Fact]
    public void LoadPatterns_HandlesMultipleCodeBlocks()
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), """
            ## Environment files
            ```
            .env
            ```

            ## Credentials
            ```
            secrets.json
            ```
            """);

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        Assert.Contains(".env", service.Patterns);
        Assert.Contains("secrets.json", service.Patterns);
    }

    [Fact]
    public void LoadPatterns_ReturnsEmptyWhenFileNotExists()
    {
        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        Assert.Empty(service.Patterns);
    }

    #endregion

    #region Pattern Matching

    [Theory]
    [InlineData(".env", ".env")]
    [InlineData(".env.local", ".env.*")]
    [InlineData(".env.production", ".env.*")]
    [InlineData(".env.development", ".env.*")]
    [InlineData(".env.test", ".env.*")]
    public void IsPathOffLimits_MatchesEnvPatterns(string path, string pattern)
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), $"""
            ```
            {pattern}
            ```
            """);

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        var result = service.IsPathOffLimits(path);
        Assert.NotNull(result);
        Assert.Equal(pattern, result);
    }

    [Theory]
    [InlineData("secrets.json", "**/secrets.json")]
    [InlineData("config/secrets.json", "**/secrets.json")]
    [InlineData("src/config/secrets.json", "**/secrets.json")]
    [InlineData("deep/nested/path/secrets.json", "**/secrets.json")]
    public void IsPathOffLimits_MatchesDoubleStarPatterns(string path, string pattern)
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), $"""
            ```
            {pattern}
            ```
            """);

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        var result = service.IsPathOffLimits(path);
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("credentials.json", "**/credentials.*")]
    [InlineData("credentials.yaml", "**/credentials.*")]
    [InlineData("credentials.yml", "**/credentials.*")]
    [InlineData("src/api/credentials.xml", "**/credentials.*")]
    public void IsPathOffLimits_MatchesWildcardExtensions(string path, string pattern)
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), $"""
            ```
            {pattern}
            ```
            """);

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        var result = service.IsPathOffLimits(path);
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("README.md", ".env")]
    [InlineData("config.json", "secrets.json")]
    [InlineData("src/main.cs", "**/*.key")]
    [InlineData("envfile.txt", ".env")]
    [InlineData("my.env.bak", ".env")]
    public void IsPathOffLimits_ReturnsNullForNonMatches(string path, string pattern)
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), $"""
            ```
            {pattern}
            ```
            """);

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        var result = service.IsPathOffLimits(path);
        Assert.Null(result);
    }

    [Theory]
    [InlineData("config/secrets.json")]
    [InlineData("config\\secrets.json")]
    public void IsPathOffLimits_HandlesBothSlashTypes(string path)
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), """
            ```
            **/secrets.json
            ```
            """);

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        var result = service.IsPathOffLimits(path);
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("./config/secrets.json")]
    [InlineData("/config/secrets.json")]
    public void IsPathOffLimits_NormalizesLeadingPathComponents(string path)
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), """
            ```
            **/secrets.json
            ```
            """);

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        var result = service.IsPathOffLimits(path);
        Assert.NotNull(result);
    }

    [Theory]
    [InlineData("private.key", "**/*.key")]
    [InlineData("server.pem", "**/*.pem")]
    [InlineData("cert.pfx", "**/*.pfx")]
    [InlineData("id_rsa", "**/id_rsa")]
    [InlineData("id_ed25519", "**/id_ed25519")]
    public void IsPathOffLimits_MatchesCryptoKeyPatterns(string path, string pattern)
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), $"""
            ```
            {pattern}
            ```
            """);

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        var result = service.IsPathOffLimits(path);
        Assert.NotNull(result);
    }

    [Fact]
    public void IsPathOffLimits_MatchesSimplePatternAgainstFilename()
    {
        // Pattern without path separators should match against filename too
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), """
            ```
            .env
            ```
            """);

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        // Should match .env even in subdirectories
        Assert.NotNull(service.IsPathOffLimits(".env"));
        Assert.NotNull(service.IsPathOffLimits("config/.env"));
        Assert.NotNull(service.IsPathOffLimits("src/config/.env"));
    }

    #endregion

    #region Validate Literal Paths

    [Fact]
    public void ValidateLiteralPaths_ReportsMissingFiles()
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), """
            ```
            existing.txt
            missing.txt
            *.wildcard
            ```
            """);

        // Create only existing.txt
        File.WriteAllText(Path.Combine(_testDir, "existing.txt"), "");

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        var missing = service.ValidateLiteralPaths(_testDir).ToList();

        Assert.Single(missing);
        Assert.Contains("missing.txt", missing);
    }

    [Fact]
    public void ValidateLiteralPaths_SkipsWildcardPatterns()
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), """
            ```
            **/*.key
            .env.*
            secrets?.json
            ```
            """);

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        var missing = service.ValidateLiteralPaths(_testDir).ToList();

        // Wildcard patterns should not be validated as literal paths
        Assert.Empty(missing);
    }

    [Fact]
    public void ValidateLiteralPaths_RecognizesExistingDirectories()
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), """
            ```
            existingdir
            missingdir
            ```
            """);

        Directory.CreateDirectory(Path.Combine(_testDir, "existingdir"));

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        var missing = service.ValidateLiteralPaths(_testDir).ToList();

        Assert.Single(missing);
        Assert.Contains("missingdir", missing);
    }

    #endregion

    #region Check Command

    [Theory]
    [InlineData("cat secrets.json", "secrets.json")]
    [InlineData("cat ./secrets.json", "secrets.json")]
    [InlineData("head .env", ".env")]
    [InlineData("tail .env.local", ".env.local")]
    [InlineData("type secrets.json", "secrets.json")]
    [InlineData("Get-Content secrets.json", "secrets.json")]
    public void CheckCommand_DetectsOffLimitsInReadCommands(string command, string expectedPath)
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), """
            ```
            secrets.json
            .env
            .env.*
            ```
            """);

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        var (isBlocked, matchedPath, _) = service.CheckCommand(command);

        Assert.True(isBlocked);
        Assert.Contains(expectedPath, matchedPath!);
    }

    [Theory]
    [InlineData("echo 'data' > secrets.json")]
    [InlineData("rm secrets.json")]
    [InlineData("rm -f .env.local")]
    [InlineData("tee secrets.json")]
    public void CheckCommand_DetectsOffLimitsInWriteDeleteCommands(string command)
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), """
            ```
            secrets.json
            .env
            .env.*
            ```
            """);

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        var (isBlocked, _, _) = service.CheckCommand(command);

        Assert.True(isBlocked);
    }

    [Theory]
    [InlineData("cat README.md")]
    [InlineData("echo hello > output.txt")]
    [InlineData("rm temp.log")]
    [InlineData("ls -la")]
    [InlineData("dotnet build")]
    public void CheckCommand_AllowsSafeCommands(string command)
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), """
            ```
            secrets.json
            .env
            ```
            """);

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        var (isBlocked, _, _) = service.CheckCommand(command);

        Assert.False(isBlocked);
    }

    [Fact]
    public void CheckCommand_DetectsQuotedPaths()
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), """
            ```
            secrets.json
            ```
            """);

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        var (isBlocked1, _, _) = service.CheckCommand("cat 'secrets.json'");
        var (isBlocked2, _, _) = service.CheckCommand("cat \"secrets.json\"");

        Assert.True(isBlocked1);
        Assert.True(isBlocked2);
    }

    #endregion

    #region File Existence

    [Fact]
    public void OffLimitsFileExists_ReturnsTrueWhenExists()
    {
        File.WriteAllText(Path.Combine(_dydoDir, "files-off-limits.md"), "# Off limits");

        var service = new OffLimitsService();
        service.LoadPatterns(_testDir);

        Assert.True(service.OffLimitsFileExists(_testDir));
    }

    [Fact]
    public void OffLimitsFileExists_ReturnsFalseWhenMissing()
    {
        var service = new OffLimitsService();

        Assert.False(service.OffLimitsFileExists(_testDir));
    }

    #endregion
}
