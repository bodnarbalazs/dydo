namespace DynaDocs.Commands;

using System.CommandLine;
using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;
using DynaDocs.Services;
using DynaDocs.Utils;

public static class RolesCommand
{
    public static Command Create()
    {
        var rolesCommand = new Command("roles", "Manage role definitions");

        rolesCommand.Subcommands.Add(CreateResetCommand());
        rolesCommand.Subcommands.Add(CreateListCommand());
        rolesCommand.Subcommands.Add(CreateCreateCommand());

        return rolesCommand;
    }

    private static Command CreateResetCommand()
    {
        var allOption = new Option<bool>("--all")
        {
            Description = "Remove all role files (including custom) before regenerating base roles"
        };
        var resetCommand = new Command("reset", "Regenerate base role definition files");
        resetCommand.Options.Add(allOption);

        resetCommand.SetAction((parseResult, _) =>
        {
            var human = Environment.GetEnvironmentVariable("DYDO_HUMAN");
            if (string.IsNullOrEmpty(human))
            {
                Console.Error.WriteLine("Error: DYDO_HUMAN not set. This command is human-only.");
                return Task.FromResult(2);
            }

            var resetAll = parseResult.GetValue(allOption);
            var basePath = PathUtils.FindProjectRoot() ?? Environment.CurrentDirectory;
            var rolesDir = Path.Combine(basePath, "dydo", "_system", "roles");

            if (resetAll)
            {
                if (Directory.Exists(rolesDir))
                {
                    foreach (var file in Directory.GetFiles(rolesDir, "*.role.json"))
                        File.Delete(file);
                }
                Console.WriteLine("Removed all role files.");
            }
            else if (Directory.Exists(rolesDir))
            {
                var service = new RoleDefinitionService();
                var existing = service.LoadRoleDefinitions(basePath);
                foreach (var role in existing.Where(r => r.Base))
                {
                    var filePath = Path.Combine(rolesDir, $"{role.Name}.role.json");
                    if (File.Exists(filePath))
                        File.Delete(filePath);
                }
                Console.WriteLine("Removed base role files.");
            }

            new RoleDefinitionService().WriteBaseRoleDefinitions(basePath);
            Console.WriteLine("Base role definitions regenerated.");
            return Task.FromResult(0);
        });

        return resetCommand;
    }

    private static Command CreateListCommand()
    {
        var listCommand = new Command("list", "List all loaded role definitions");

        listCommand.SetAction((_, _) =>
        {
            var basePath = PathUtils.FindProjectRoot() ?? Environment.CurrentDirectory;
            var service = new RoleDefinitionService();
            var roles = service.LoadRoleDefinitions(basePath);

            if (roles.Count == 0)
            {
                Console.WriteLine("No role definition files found. Using built-in defaults.");
                Console.WriteLine("Run 'dydo init' to generate role files.");
                return Task.FromResult(0);
            }

            foreach (var role in roles.OrderBy(r => r.Name))
            {
                var tag = role.Base ? "base" : "custom";
                Console.WriteLine($"  {role.Name} ({tag}) — {role.Description}");
            }

            return Task.FromResult(0);
        });

        return listCommand;
    }

    private static Command CreateCreateCommand()
    {
        var nameArg = new Argument<string>("name")
        {
            Description = "Name for the new role"
        };
        var createCommand = new Command("create", "Scaffold a new custom role definition file");
        createCommand.Arguments.Add(nameArg);

        createCommand.SetAction((parseResult, _) =>
        {
            var roleName = parseResult.GetValue(nameArg)!;
            var basePath = PathUtils.FindProjectRoot() ?? Environment.CurrentDirectory;
            var rolesDir = Path.Combine(basePath, "dydo", "_system", "roles");

            // Check for conflicts with existing roles
            var service = new RoleDefinitionService();
            var existing = service.LoadRoleDefinitions(basePath);
            var baseRoles = RoleDefinitionService.GetBaseRoleDefinitions();

            if (baseRoles.Any(r => r.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase)))
            {
                Console.Error.WriteLine($"Error: '{roleName}' conflicts with a base role name.");
                return Task.FromResult(1);
            }

            if (existing.Any(r => r.Name.Equals(roleName, StringComparison.OrdinalIgnoreCase)))
            {
                Console.Error.WriteLine($"Error: A role named '{roleName}' already exists.");
                return Task.FromResult(1);
            }

            // Scaffold minimal role definition
            var role = new RoleDefinition
            {
                Name = roleName,
                Description = "",
                Base = false,
                WritablePaths = ["dydo/agents/{self}/**"],
                ReadOnlyPaths = [],
                TemplateFile = $"mode-{roleName}.template.md",
                DenialHint = null,
                Constraints = []
            };

            Directory.CreateDirectory(rolesDir);
            var filePath = Path.Combine(rolesDir, $"{roleName}.role.json");
            var json = JsonSerializer.Serialize(role, DydoConfigJsonContext.Default.RoleDefinition);
            File.WriteAllText(filePath, json);

            // Run validation on the created file
            var validator = new ValidationService();
            var issues = validator.ValidateRoleFile(basePath, filePath);
            if (issues.Count > 0)
            {
                Console.WriteLine("Validation warnings for new role:");
                foreach (var issue in issues)
                    Console.WriteLine($"  [{issue.Severity}] {issue.Message}");
                Console.WriteLine();
            }

            Console.WriteLine($"Created: dydo/_system/roles/{roleName}.role.json");
            Console.WriteLine();
            Console.WriteLine("Next steps:");
            Console.WriteLine($"  1. Edit the role file to set description, writable/readOnly paths, and denialHint");
            Console.WriteLine($"  2. Optionally create a mode template: dydo/_system/templates/mode-{roleName}.template.md");

            return Task.FromResult(0);
        });

        return createCommand;
    }
}
