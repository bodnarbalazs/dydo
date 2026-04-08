namespace DynaDocs.Tests.Commands;

using DynaDocs.Services;

[Collection("Integration")]
public class RolesResetCommandTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _originalDir;
    private readonly string? _originalHuman;

    public RolesResetCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-rolesreset-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _originalDir = Environment.CurrentDirectory;
        _originalHuman = Environment.GetEnvironmentVariable("DYDO_HUMAN");

        var rolesDir = Path.Combine(_testDir, "dydo", "_system", "roles");
        Directory.CreateDirectory(rolesDir);
        new RoleDefinitionService().WriteBaseRoleDefinitions(_testDir);

        Environment.CurrentDirectory = _testDir;
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDir;
        Environment.SetEnvironmentVariable("DYDO_HUMAN", _originalHuman);
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }
        catch (IOException) { }
    }

    [Fact]
    public void Reset_FailsWhenDydoHumanNotSet()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", null);

        var command = DynaDocs.Commands.RolesCommand.Create();
        var result = command.Parse("reset").Invoke();

        Assert.Equal(2, result);
    }

    [Fact]
    public void ResetAll_DeletesAllRoleFilesAndRegenerates()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        var rolesDir = Path.Combine(_testDir, "dydo", "_system", "roles");

        // Add a custom role file
        File.WriteAllText(Path.Combine(rolesDir, "custom.role.json"), "{}");
        var beforeCount = Directory.GetFiles(rolesDir, "*.role.json").Length;
        Assert.True(beforeCount > 1);

        var command = DynaDocs.Commands.RolesCommand.Create();
        var result = command.Parse("reset --all").Invoke();

        Assert.Equal(0, result);

        // Custom role should be gone, only base roles remain
        var files = Directory.GetFiles(rolesDir, "*.role.json");
        Assert.DoesNotContain(files, f => Path.GetFileName(f) == "custom.role.json");
        Assert.True(files.Length > 0); // Base roles regenerated
    }

    [Fact]
    public void ResetAll_WhenRolesDirDoesNotExist_StillRegenerates()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        var rolesDir = Path.Combine(_testDir, "dydo", "_system", "roles");
        Directory.Delete(rolesDir, true);

        var command = DynaDocs.Commands.RolesCommand.Create();
        var result = command.Parse("reset --all").Invoke();

        Assert.Equal(0, result);
        Assert.True(Directory.Exists(rolesDir));
        Assert.True(Directory.GetFiles(rolesDir, "*.role.json").Length > 0);
    }

    [Fact]
    public void Reset_WithoutAll_DeletesOnlyBaseRoles()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        var rolesDir = Path.Combine(_testDir, "dydo", "_system", "roles");

        // Add a custom role file
        File.WriteAllText(Path.Combine(rolesDir, "custom.role.json"),
            """{"name":"custom","description":"test","base":false,"writablePaths":[],"readOnlyPaths":[],"templateFile":"","denialHint":null,"constraints":[]}""");

        var command = DynaDocs.Commands.RolesCommand.Create();
        var result = command.Parse("reset").Invoke();

        Assert.Equal(0, result);

        // Custom role should still exist
        Assert.True(File.Exists(Path.Combine(rolesDir, "custom.role.json")));
        // Base roles should be regenerated
        Assert.True(Directory.GetFiles(rolesDir, "*.role.json").Length > 1);
    }

    [Fact]
    public void Reset_WithoutAll_WhenRolesDirDoesNotExist_StillRegenerates()
    {
        Environment.SetEnvironmentVariable("DYDO_HUMAN", "testuser");
        var rolesDir = Path.Combine(_testDir, "dydo", "_system", "roles");
        Directory.Delete(rolesDir, true);

        var command = DynaDocs.Commands.RolesCommand.Create();
        var result = command.Parse("reset").Invoke();

        Assert.Equal(0, result);
        Assert.True(Directory.Exists(rolesDir));
    }

    [Fact]
    public void List_PrintsRolesWhenPresent()
    {
        var (exitCode, output, _) = ConsoleCapture.All(() =>
        {
            var command = DynaDocs.Commands.RolesCommand.Create();
            return command.Parse("list").Invoke();
        });
        Assert.Equal(0, exitCode);
        Assert.Contains("base", output);
    }

    [Fact]
    public void List_PrintsMessageWhenNoRoles()
    {
        var rolesDir = Path.Combine(_testDir, "dydo", "_system", "roles");
        foreach (var file in Directory.GetFiles(rolesDir, "*.role.json"))
            File.Delete(file);

        var (exitCode, output, _) = ConsoleCapture.All(() =>
        {
            var command = DynaDocs.Commands.RolesCommand.Create();
            return command.Parse("list").Invoke();
        });
        Assert.Equal(0, exitCode);
        Assert.Contains("No role definition files found", output);
    }

    [Fact]
    public void Create_ReturnsCommandWithAllSubcommands()
    {
        var command = DynaDocs.Commands.RolesCommand.Create();

        Assert.Equal("roles", command.Name);
        Assert.Equal(3, command.Subcommands.Count);
        Assert.Contains(command.Subcommands, c => c.Name == "reset");
        Assert.Contains(command.Subcommands, c => c.Name == "list");
        Assert.Contains(command.Subcommands, c => c.Name == "create");
    }
}
