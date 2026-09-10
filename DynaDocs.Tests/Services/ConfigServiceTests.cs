namespace DynaDocs.Tests.Services;

using DynaDocs.Models;
using DynaDocs.Services;

public class ConfigServiceTests : IDisposable
{
    private readonly string _testDir;

    public ConfigServiceTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-config-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);

        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            {
                "version": 1,
                "structure": { "root": "dydo" }
            }
            """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void GetChangelogPath_ReturnsCorrectPath()
    {
        var service = new ConfigService();

        var result = service.GetChangelogPath(_testDir);

        Assert.EndsWith(Path.Combine("project", "changelog"), result);
    }

    [Fact]
    public void FindConfigFile_ReturnsNull_WhenNoConfigExists()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), "dydo-empty-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(emptyDir);
        try
        {
            var service = new ConfigService();
            Assert.Null(service.FindConfigFile(emptyDir));
        }
        finally
        {
            Directory.Delete(emptyDir, true);
        }
    }

    [Fact]
    public void FindConfigFile_CachesResult()
    {
        var service = new ConfigService();

        var first = service.FindConfigFile(_testDir);
        var second = service.FindConfigFile(_testDir);

        Assert.Equal(first, second);
    }

    [Fact]
    public void FindConfigFile_WalksUpDirectoryTree()
    {
        var subDir = Path.Combine(_testDir, "a", "b", "c");
        Directory.CreateDirectory(subDir);

        var service = new ConfigService();
        var result = service.FindConfigFile(subDir);

        Assert.NotNull(result);
        Assert.EndsWith("dydo.json", result);
    }

    [Fact]
    public void LoadConfig_ReturnsNull_WhenNoConfigFile()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), "dydo-noconf-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(emptyDir);
        try
        {
            var service = new ConfigService();
            Assert.Null(service.LoadConfig(emptyDir));
        }
        finally
        {
            Directory.Delete(emptyDir, true);
        }
    }

    [Fact]
    public void LoadConfig_ReturnsNull_WhenInvalidJson()
    {
        var badDir = Path.Combine(Path.GetTempPath(), "dydo-bad-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(badDir);
        File.WriteAllText(Path.Combine(badDir, "dydo.json"), "not valid json {{{");
        try
        {
            var service = new ConfigService();
            Assert.Null(service.LoadConfig(badDir));
        }
        finally
        {
            Directory.Delete(badDir, true);
        }
    }

    [Fact]
    public void LoadConfigStrict_DistinguishesMalformedFromMissingConfiguration()
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), "{ not-json");

        var error = Assert.Throws<InvalidDataException>(
            () => new ConfigService().LoadConfigStrict(_testDir));

        Assert.Contains("dydo.json", error.Message);
        Assert.Contains("Invalid JSON", error.Message);
    }

    [Theory]
    [InlineData("\"skills\": []", "skills must be an object")]
    [InlineData("\"skills\": { \"custom\": {} }", "enabled")]
    [InlineData("\"skills\": { \"Custom\": { \"enabled\": true } }", "Custom")]
    [InlineData("\"skills\": { \"custom\": { \"enabled\": true, \"extra\": 1 } }", "extra")]
    [InlineData("\"skills\": { \"custom\": { \"enabled\": true, \"origin\": 1 } }", "origin")]
    public void LoadConfigStrict_RejectsMalformedSwitchboard(string skillsJson, string expected)
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), $$"""
            {
              "version": 1,
              "structure": { "root": "dydo" },
              {{skillsJson}}
            }
            """);

        var error = Assert.Throws<InvalidDataException>(
            () => new ConfigService().LoadConfigStrict(_testDir));

        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void SaveConfig_OrdersSkillSwitchesAndGeneratedResourcesOrdinally()
    {
        var config = new DydoConfig
        {
            Skills = new Dictionary<string, SkillSwitchConfig>
            {
                ["zeta"] = new() { Enabled = true, Resources = ["two", "one"] },
                ["alpha"] = new() { Enabled = false }
            }
        };
        var path = Path.Combine(_testDir, "ordered.json");

        new ConfigService().SaveConfig(config, path);

        var json = File.ReadAllText(path);
        Assert.True(json.IndexOf("\"alpha\"", StringComparison.Ordinal)
            < json.IndexOf("\"zeta\"", StringComparison.Ordinal));
        Assert.True(json.IndexOf("\"one\"", StringComparison.Ordinal)
            < json.IndexOf("\"two\"", StringComparison.Ordinal));
    }

    [Fact]
    public void LoadConfig_ReturnsConfig_WhenValid()
    {
        var service = new ConfigService();
        var config = service.LoadConfig(_testDir);

        Assert.NotNull(config);
        Assert.Equal(1, config.Version);
    }

    [Fact]
    public void SaveConfig_WritesFile()
    {
        var service = new ConfigService();
        var config = new DydoConfig
        {
            Version = 2,
            Structure = new StructureConfig { Root = "dydo" },
            Integrations = new Dictionary<string, bool>()
        };

        var path = Path.Combine(_testDir, "saved.json");
        service.SaveConfig(config, path);

        Assert.True(File.Exists(path));
        var content = File.ReadAllText(path);
        Assert.Contains("\"version\"", content);
    }

    [Fact]
    public void SaveConfig_OmitsAbsentTestingConfiguration()
    {
        var path = Path.Combine(_testDir, "without-testing.json");

        new ConfigService().SaveConfig(new DydoConfig(), path);

        Assert.DoesNotContain("\"testing\"", File.ReadAllText(path));
    }

    [Fact]
    public void LoadConfigStrict_PreservesValidTestingRunnerAndSecondSaveIsByteIdentical()
    {
        var path = Path.Combine(_testDir, "dydo.json");
        var service = new ConfigService();
        service.SaveConfig(new DydoConfig
        {
            Testing = new TestingConfig { Runner = ["runner with spaces", "", "fixed space", "固定λ"] }
        }, path);
        var first = File.ReadAllBytes(path);

        var config = service.LoadConfigStrict(_testDir)!;
        Assert.Equal(["runner with spaces", "", "fixed space", "固定λ"], config.Testing!.Runner);
        service.SaveConfig(config, path);

        Assert.Equal(first, File.ReadAllBytes(path));
    }

    [Theory]
    [InlineData("{\"testing\":[]}")]
    [InlineData("{\"testing\":{}}")]
    [InlineData("{\"testing\":{\"runner\":[]}}")]
    [InlineData("{\"testing\":{\"runner\":[\"\"]}}")]
    [InlineData("{\"testing\":{\"runner\":[1]}}")]
    [InlineData("{\"testing\":{\"runner\":[\"runner\",\"\\u0000\"]}}")]
    public void LoadConfigStrict_RejectsInvalidTestingRunner(string json)
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), json);

        var error = Assert.Throws<InvalidDataException>(() => new ConfigService().LoadConfigStrict(_testDir));

        Assert.Contains("dydo.json testing.runner", error.Message);
    }

    [Fact]
    public void LoadConfigStrict_PropagatesAnUnreadableConfiguration()
    {
        var path = Path.Combine(_testDir, "dydo.json");
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        Assert.Throws<IOException>(() => new ConfigService().LoadConfigStrict(_testDir));
    }

    // The six injected boundaries of the atomic save, each spelled here rather than taken from a
    // production global, so one test replaces exactly the boundary it fails.
    private static void SaveWith(
        string path,
        string temporary,
        Action<FileStream, byte[]>? writeAll = null,
        Action<FileStream>? durableFlush = null,
        Action<FileStream>? close = null,
        Action<string, string>? replace = null)
        => new ConfigService().SaveConfig(new DydoConfig { Version = 7 }, path,
            new ConfigSaveOperations(
                _ => temporary,
                candidate => new FileStream(candidate, FileMode.CreateNew, FileAccess.Write, FileShare.None),
                writeAll ?? ((stream, bytes) => stream.Write(bytes)),
                durableFlush ?? (stream => stream.Flush(flushToDisk: true)),
                close ?? (stream => stream.Dispose()),
                replace ?? ((source, target) => File.Move(source, target, overwrite: true))));

    [Fact]
    public void SaveConfig_Success_AtomicallyReplacesAndLeavesNoTemporarySibling()
    {
        var path = Path.Combine(_testDir, "atomic-success.json");
        var temporary = path + ".owned.tmp";
        File.WriteAllBytes(path, "ORIGINAL"u8.ToArray());

        SaveWith(path, temporary);

        Assert.Contains("\"version\": 7", File.ReadAllText(path));
        Assert.False(File.Exists(temporary));
    }

    [Fact]
    public void SaveConfig_PartialTemporaryWriteFailure_PreservesOriginalAndCleansTemporarySibling()
    {
        var path = Path.Combine(_testDir, "partial-write.json");
        var temporary = path + ".owned.tmp";
        var original = "ORIGINAL-PARTIAL-WRITE"u8.ToArray();
        File.WriteAllBytes(path, original);

        var failure = Assert.Throws<IOException>(() => SaveWith(path, temporary, writeAll: (stream, bytes) =>
        {
            stream.Write(bytes.AsSpan(0, 3));
            throw new IOException("injected partial write failure");
        }));

        Assert.Equal("injected partial write failure", failure.Message);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.False(File.Exists(temporary));
    }

    [Fact]
    public void SaveConfig_FlushFailure_PreservesOriginalAndCleansTemporarySibling()
    {
        var path = Path.Combine(_testDir, "flush.json");
        var temporary = path + ".owned.tmp";
        var original = "ORIGINAL-FLUSH"u8.ToArray();
        File.WriteAllBytes(path, original);

        var failure = Assert.Throws<IOException>(() => SaveWith(path, temporary,
            durableFlush: _ => throw new IOException("injected durable flush failure")));

        Assert.Equal("injected durable flush failure", failure.Message);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.False(File.Exists(temporary));
    }

    [Fact]
    public void SaveConfig_CloseFailure_PreservesOriginalAndCleansTemporarySibling()
    {
        var path = Path.Combine(_testDir, "close.json");
        var temporary = path + ".owned.tmp";
        var original = "ORIGINAL-CLOSE"u8.ToArray();
        File.WriteAllBytes(path, original);

        var failure = Assert.Throws<IOException>(() => SaveWith(path, temporary, close: stream =>
        {
            stream.Dispose();
            throw new IOException("injected close failure");
        }));

        Assert.Equal("injected close failure", failure.Message);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.False(File.Exists(temporary));
    }

    [Fact]
    public void SaveConfig_ReplacementFailure_PreservesOriginalAndCleansTemporarySibling()
    {
        var path = Path.Combine(_testDir, "replace.json");
        var temporary = path + ".owned.tmp";
        var original = "ORIGINAL-REPLACE"u8.ToArray();
        File.WriteAllBytes(path, original);

        var failure = Assert.Throws<IOException>(() => SaveWith(path, temporary,
            replace: (_, _) => throw new IOException("injected replacement failure")));

        Assert.Equal("injected replacement failure", failure.Message);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.False(File.Exists(temporary));
    }

    [Fact]
    public void SaveConfig_TemporaryNameCollision_PreservesBothFiles()
    {
        var path = Path.Combine(_testDir, "collision.json");
        var temporary = path + ".collision.tmp";
        var original = "ORIGINAL-COLLISION"u8.ToArray();
        var collision = "PREEXISTING-SIBLING"u8.ToArray();
        File.WriteAllBytes(path, original);
        File.WriteAllBytes(temporary, collision);

        var failure = Assert.Throws<IOException>(() => SaveWith(path, temporary));

        Assert.Contains("already exists", failure.Message);
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Equal(collision, File.ReadAllBytes(temporary));
    }

    // The two production boundaries the injected tests above cannot reach: where the sibling
    // goes, and how it is opened.
    [Fact]
    public void TemporarySiblingPath_IsAUniqueTmpBesideTheTarget()
    {
        var target = Path.Combine(_testDir, "dydo.json");

        var first = ConfigService.TemporarySiblingPath(target);
        var second = ConfigService.TemporarySiblingPath(target);

        Assert.Equal(_testDir, Path.GetDirectoryName(first));
        Assert.Matches(@"^dydo\.json\.[0-9a-f]{32}\.tmp$", Path.GetFileName(first));
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void CreateNewSibling_RefusesAnExistingFile()
    {
        var sibling = Path.Combine(_testDir, "dydo.json.taken.tmp");
        File.WriteAllBytes(sibling, "PREEXISTING"u8.ToArray());

        Assert.Throws<IOException>(() => ConfigService.CreateNewSibling(sibling));

        Assert.Equal("PREEXISTING"u8.ToArray(), File.ReadAllBytes(sibling));
    }

    [Fact]
    public void CreateNewSibling_OpensWriteOnlyAndExcludesOtherHandles()
    {
        var sibling = Path.Combine(_testDir, "dydo.json.fresh.tmp");

        using var stream = ConfigService.CreateNewSibling(sibling);

        Assert.True(stream.CanWrite);
        Assert.False(stream.CanRead);
        Assert.Throws<IOException>(() => File.OpenRead(sibling));
    }

    [Fact]
    public void GetProjectRoot_ReturnsNull_WhenNoConfig()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), "dydo-noroot-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(emptyDir);
        try
        {
            var service = new ConfigService();
            Assert.Null(service.GetProjectRoot(emptyDir));
        }
        finally
        {
            Directory.Delete(emptyDir, true);
        }
    }

    [Fact]
    public void GetProjectRoot_ReturnsDirectory_WhenConfigExists()
    {
        var service = new ConfigService();
        var root = service.GetProjectRoot(_testDir);

        Assert.NotNull(root);
        Assert.Equal(_testDir, root);
    }

    [Fact]
    public void GetDydoRoot_UsesConfiguredRoot()
    {
        var service = new ConfigService();
        var dydoRoot = service.GetDydoRoot(_testDir);

        Assert.EndsWith("dydo", dydoRoot);
    }

    [Fact]
    public void GetDydoRoot_FallsBackToStartPath_WhenNoConfig()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), "dydo-fallback-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(emptyDir);
        try
        {
            var service = new ConfigService();
            var result = service.GetDydoRoot(emptyDir);

            Assert.Equal(Path.Combine(emptyDir, "dydo"), result);
        }
        finally
        {
            Directory.Delete(emptyDir, true);
        }
    }

    [Fact]
    public void GetDocsPath_ReturnsDydoRoot()
    {
        var service = new ConfigService();
        var dydoRoot = service.GetDydoRoot(_testDir);
        var docsPath = service.GetDocsPath(_testDir);

        Assert.Equal(dydoRoot, docsPath);
    }

    [Fact]
    public void GetAuditPath_ReturnsSystemAuditSubfolder()
    {
        var service = new ConfigService();
        var result = service.GetAuditPath(_testDir);

        Assert.Contains("_system", result);
        Assert.Contains("audit", result);
    }

    [Fact]
    public void LoadConfig_LegacyWorkPaths_AreIgnored()
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            {
                "version": 1,
                "structure": {
                    "root": "docs",
                    "tasks": "custom/tasks",
                    "issues": "custom/issues"
                }
            }
            """);

        var config = new ConfigService().LoadConfig(_testDir);

        Assert.NotNull(config);
        Assert.Equal("docs", config!.Structure.Root);
        Assert.DoesNotContain("Tasks", typeof(StructureConfig).GetProperties().Select(p => p.Name));
        Assert.DoesNotContain("Issues", typeof(StructureConfig).GetProperties().Select(p => p.Name));
    }

    [Fact]
    public void SaveConfig_DoesNotWriteLegacyWorkPaths()
    {
        var path = Path.Combine(_testDir, "saved.json");
        new ConfigService().SaveConfig(new DydoConfig(), path);

        var json = File.ReadAllText(path);
        Assert.DoesNotContain("\"tasks\"", json);
        Assert.DoesNotContain("\"issues\"", json);
    }

    [Fact]
    public void LoadConfig_LegacyAgentsSection_IsIgnored()
    {
        // The 26-agent roster runtime was removed (DR-041); DydoConfig no longer models an
        // `agents` section. A pre-DR-041 dydo.json still carrying one must load cleanly, with
        // the dead section silently dropped rather than failing deserialization.
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            {
                "version": 1,
                "structure": { "root": "dydo" },
                "agents": { "pool": ["Adele", "Brian"], "assignments": { "testuser": ["Adele"] } }
            }
            """);

        var config = new ConfigService().LoadConfig(_testDir);

        Assert.NotNull(config);
    }

    [Fact]
    public void LoadConfig_WithNudges_DeserializesCorrectly()
    {
        File.WriteAllText(Path.Combine(_testDir, "dydo.json"), """
            {
                "version": 1,
                "structure": { "root": "dydo" },
                "nudges": [
                    { "pattern": "dotnet test.*coverlet", "message": "Use gap_check.py.", "severity": "warn" },
                    { "pattern": "rm -rf", "message": "Don't do that.", "severity": "block" }
                ]
            }
            """);

        var service = new ConfigService();
        var config = service.LoadConfig(_testDir);

        Assert.NotNull(config);
        Assert.Equal(2, config!.Nudges.Count);
        Assert.Equal("dotnet test.*coverlet", config.Nudges[0].Pattern);
        Assert.Equal("warn", config.Nudges[0].Severity);
        Assert.Equal("block", config.Nudges[1].Severity);
    }

    [Fact]
    public void LoadConfig_WithoutNudges_DefaultsToEmptyList()
    {
        var service = new ConfigService();
        var config = service.LoadConfig(_testDir);

        Assert.NotNull(config);
        Assert.Empty(config!.Nudges);
    }

    // The no-shim invariant: 3.0 dropped several dydo.json sections and reads none of them back.
    // A config file written by 2.x or early 3.0 must still load, with the retired sections ignored
    // and every surviving section arriving intact. The retired key spellings live only in the
    // fixture so the residue gates over the test sources stay honest.
    [Fact]
    public void LoadConfig_ConfigFromBeforeTheSkillModelSimplification_IgnoresRetiredSections()
    {
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "config", "dydo-2x-old-keys.json"),
            Path.Combine(_testDir, "dydo.json"),
            overwrite: true);

        var config = new ConfigService().LoadConfig(_testDir);

        Assert.NotNull(config);
        Assert.Equal("dydo", config!.Structure.Root);
        Assert.Equal(2, config.Nudges.Count);
        Assert.Equal("dotnet test.*coverlet", config.Nudges[0].Pattern);
        Assert.Equal("warn", config.Nudges[0].Severity);
        Assert.Equal("rm -rf", config.Nudges[1].Pattern);
        Assert.DoesNotContain("Models", typeof(DydoConfig).GetProperties().Select(property => property.Name));
        Assert.True(config.Integrations["claude"]);
        Assert.False(config.Integrations["codex"]);
    }

}
