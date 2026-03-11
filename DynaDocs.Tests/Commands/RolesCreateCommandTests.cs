namespace DynaDocs.Tests.Commands;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Services;

public class RolesCreateCommandTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _originalDir;

    public RolesCreateCommandTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "dydo-rolescreate-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
        _originalDir = Environment.CurrentDirectory;

        // Scaffold minimal dydo structure
        var rolesDir = Path.Combine(_testDir, "dydo", "_system", "roles");
        Directory.CreateDirectory(rolesDir);

        // Write base role files so LoadRoleDefinitions works
        new RoleDefinitionService().WriteBaseRoleDefinitions(_testDir);

        Environment.CurrentDirectory = _testDir;
    }

    public void Dispose()
    {
        Environment.CurrentDirectory = _originalDir;
        try
        {
            if (Directory.Exists(_testDir))
                Directory.Delete(_testDir, true);
        }
        catch (IOException)
        {
            // Windows file locking — temp dir will be cleaned up by OS
        }
    }

    [Fact]
    public void RolesCreate_GeneratesValidJsonWithCorrectDefaults()
    {
        var command = DynaDocs.Commands.RolesCommand.Create();
        var result = command.Parse("create deploy-manager").Invoke();

        Assert.Equal(0, result);

        var filePath = Path.Combine(_testDir, "dydo", "_system", "roles", "deploy-manager.role.json");
        Assert.True(File.Exists(filePath));

        var json = File.ReadAllText(filePath);
        var role = JsonSerializer.Deserialize<RoleDefinition>(json,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.NotNull(role);
        Assert.Equal("deploy-manager", role.Name);
        Assert.False(role.Base);
        Assert.Equal(["dydo/agents/{self}/**"], role.WritablePaths);
        Assert.Empty(role.ReadOnlyPaths);
        Assert.Equal("mode-deploy-manager.template.md", role.TemplateFile);
        Assert.Empty(role.Constraints);
    }

    [Fact]
    public void RolesCreate_RejectsDuplicateNames()
    {
        // First create succeeds
        var command = DynaDocs.Commands.RolesCommand.Create();
        var result1 = command.Parse("create my-role").Invoke();
        Assert.Equal(0, result1);

        // Second create with same name fails
        command = DynaDocs.Commands.RolesCommand.Create();
        var result2 = command.Parse("create my-role").Invoke();
        Assert.Equal(1, result2);
    }

    [Fact]
    public void RolesCreate_RejectsBaseRoleNames()
    {
        var command = DynaDocs.Commands.RolesCommand.Create();
        var result = command.Parse("create code-writer").Invoke();

        Assert.Equal(1, result);

        // Verify no duplicate file was created (base one already exists)
        var rolesDir = Path.Combine(_testDir, "dydo", "_system", "roles");
        var files = Directory.GetFiles(rolesDir, "code-writer.role.json");
        Assert.Single(files);
    }

    [Fact]
    public void RolesCreate_NewRoleAppearsInList()
    {
        var command = DynaDocs.Commands.RolesCommand.Create();
        command.Parse("create infra-writer").Invoke();

        var service = new RoleDefinitionService();
        var roles = service.LoadRoleDefinitions(_testDir);

        Assert.Contains(roles, r => r.Name == "infra-writer");
    }
}
