namespace DynaDocs.Tests.Integration;

using System.Text.Json;
using DynaDocs.Commands;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// End-to-end coverage for the scanExclude invariants check/fix loop:
/// removing a dydo-internal entry from dydo.json must surface a check error;
/// dydo fix must restore it; subsequent dydo check must be clean.
/// </summary>
[Collection("Integration")]
public class ScanExcludeInvariantsTests : IntegrationTestBase
{
    [Fact]
    public async Task FreshInit_PopulatesScanExcludeWithInvariants()
    {
        var initResult = await InitProjectAsync();
        initResult.AssertSuccess();

        var config = LoadConfig();
        foreach (var invariant in ConfigFactory.DydoInternalScanExclude)
            Assert.Contains(invariant, config.ScanExclude);
    }

    [Fact]
    public async Task Check_ReportsErrorWhenInvariantIsMissing()
    {
        var initResult = await InitProjectAsync();
        initResult.AssertSuccess();

        // Drop a dydo-internal entry — simulating manual edit by the user.
        MutateConfig(c => c.ScanExclude.Remove("_system/.local/"));

        var checkResult = await CheckAsync(DydoDir);

        Assert.Contains("scanExclude is missing required entry", checkResult.Stdout + checkResult.Stderr);
        Assert.Contains("_system/.local/", checkResult.Stdout + checkResult.Stderr);
        checkResult.AssertExitCode(ExitCodes.ValidationErrors);
    }

    [Fact]
    public async Task Fix_RestoresMissingInvariantPreservingUserEntries()
    {
        var initResult = await InitProjectAsync();
        initResult.AssertSuccess();

        MutateConfig(c =>
        {
            c.ScanExclude.Remove("_system/.local/");
            c.ScanExclude.Add("vendor/");
        });

        var fixResult = await RunAsync(FixCommand.Create(), DydoDir);
        fixResult.AssertSuccess();

        var restored = LoadConfig();
        Assert.Contains("_system/.local/", restored.ScanExclude);
        Assert.Contains("vendor/", restored.ScanExclude);

        // After fix, check must be clean for the invariants.
        var checkResult = await CheckAsync(DydoDir);
        Assert.DoesNotContain("scanExclude is missing required entry", checkResult.Stdout + checkResult.Stderr);
    }

    private string ConfigPath => Path.Combine(TestDir, "dydo.json");

    private DydoConfig LoadConfig()
    {
        var json = File.ReadAllText(ConfigPath);
        return JsonSerializer.Deserialize(json, DydoConfigJsonContext.Default.DydoConfig)!;
    }

    private void MutateConfig(Action<DydoConfig> mutate)
    {
        var config = LoadConfig();
        mutate(config);
        var json = JsonSerializer.Serialize(config, DydoConfigJsonContext.Default.DydoConfig);
        File.WriteAllText(ConfigPath, json);
    }
}
