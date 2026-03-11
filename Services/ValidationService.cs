namespace DynaDocs.Services;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;

public class ValidationService : IValidationService
{
    public List<ValidationIssue> ValidateSystem(string basePath)
    {
        var issues = new List<ValidationIssue>();

        ValidateDydoJson(basePath, issues);

        var pathSets = LoadPathSets(basePath);
        ValidateRoleFiles(basePath, pathSets, issues);
        ValidateAgentStates(basePath, issues);

        return issues;
    }

    public List<ValidationIssue> ValidateRoleFile(string basePath, string roleFilePath)
    {
        var issues = new List<ValidationIssue>();
        var pathSets = LoadPathSets(basePath);
        ValidateSingleRoleFile(basePath, roleFilePath, pathSets, issues);
        return issues;
    }

    private static void ValidateDydoJson(string basePath, List<ValidationIssue> issues)
    {
        var configPath = Path.Combine(basePath, "dydo.json");
        if (!File.Exists(configPath))
        {
            issues.Add(new ValidationIssue
            {
                Severity = "error",
                File = "dydo.json",
                Message = "dydo.json not found."
            });
            return;
        }

        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize(json, DydoConfigJsonContext.Default.DydoConfig);
            if (config == null)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "error",
                    File = "dydo.json",
                    Message = "Failed to deserialize dydo.json."
                });
            }
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue
            {
                Severity = "error",
                File = "dydo.json",
                Message = $"Invalid JSON: {ex.Message}"
            });
        }
    }

    private static void ValidateRoleFiles(string basePath, Dictionary<string, List<string>> pathSets, List<ValidationIssue> issues)
    {
        var rolesDir = Path.Combine(basePath, "dydo", "_system", "roles");
        if (!Directory.Exists(rolesDir))
            return;

        foreach (var file in Directory.GetFiles(rolesDir, "*.role.json"))
        {
            ValidateSingleRoleFile(basePath, file, pathSets, issues);
        }
    }

    private static void ValidateSingleRoleFile(string basePath, string filePath, Dictionary<string, List<string>> pathSets, List<ValidationIssue> issues)
    {
        var relPath = Path.GetRelativePath(basePath, filePath).Replace('\\', '/');

        string json;
        try
        {
            json = File.ReadAllText(filePath);
        }
        catch (Exception ex)
        {
            issues.Add(new ValidationIssue
            {
                Severity = "error",
                File = relPath,
                Message = $"Cannot read file: {ex.Message}"
            });
            return;
        }

        RoleDefinition? role;
        try
        {
            role = JsonSerializer.Deserialize(json, DydoConfigJsonContext.Default.RoleDefinition);
        }
        catch (JsonException ex)
        {
            issues.Add(new ValidationIssue
            {
                Severity = "error",
                File = relPath,
                Message = $"Invalid JSON: {ex.Message}"
            });
            return;
        }

        if (role == null)
        {
            issues.Add(new ValidationIssue
            {
                Severity = "error",
                File = relPath,
                Message = "Deserialized to null."
            });
            return;
        }

        // Validate using existing service
        var service = new RoleDefinitionService();
        if (!service.ValidateRoleDefinition(role, out var errors))
        {
            foreach (var error in errors)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "error",
                    File = relPath,
                    Message = error
                });
            }
        }

        // Check constraint messages are present
        foreach (var constraint in role.Constraints)
        {
            if (string.IsNullOrWhiteSpace(constraint.Message))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "error",
                    File = relPath,
                    Message = $"Constraint of type '{constraint.Type}' has empty message."
                });
            }
        }

        // Check path set references resolve
        foreach (var path in role.WritablePaths.Concat(role.ReadOnlyPaths))
        {
            if (path.StartsWith('{') && path.EndsWith('}'))
            {
                var setName = path[1..^1];
                if (setName != "self" && !pathSets.ContainsKey(setName))
                {
                    issues.Add(new ValidationIssue
                    {
                        Severity = "warning",
                        File = relPath,
                        Message = $"Path set reference '{{{setName}}}' not found in dydo.json pathSets."
                    });
                }
            }
        }

        // Check referenced template file exists
        if (!string.IsNullOrWhiteSpace(role.TemplateFile))
        {
            var templateInTemplates = Path.Combine(basePath, "Templates", role.TemplateFile);
            var templateInSystem = Path.Combine(basePath, "dydo", "_system", "templates", role.TemplateFile);
            if (!File.Exists(templateInTemplates) && !File.Exists(templateInSystem))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "warning",
                    File = relPath,
                    Message = $"Referenced template '{role.TemplateFile}' not found in Templates/ or dydo/_system/templates/."
                });
            }
        }

        // DenialHint warning for roles with write restrictions
        if (string.IsNullOrWhiteSpace(role.DenialHint) && !role.Base)
        {
            issues.Add(new ValidationIssue
            {
                Severity = "warning",
                File = relPath,
                Message = "Custom role has no denialHint. Consider adding one for better error messages."
            });
        }
    }

    private static void ValidateAgentStates(string basePath, List<ValidationIssue> issues)
    {
        var agentsDir = Path.Combine(basePath, "dydo", "agents");
        if (!Directory.Exists(agentsDir))
            return;

        var registry = new AgentRegistry();
        foreach (var agentDir in Directory.GetDirectories(agentsDir))
        {
            var statePath = Path.Combine(agentDir, "state.md");
            if (!File.Exists(statePath))
                continue;

            var agentName = Path.GetFileName(agentDir);
            var state = registry.GetAgentState(agentName);
            if (state == null)
            {
                issues.Add(new ValidationIssue
                {
                    Severity = "error",
                    File = $"dydo/agents/{agentName}/state.md",
                    Message = "Agent state file failed to parse."
                });
            }
        }
    }

    private static Dictionary<string, List<string>> LoadPathSets(string basePath)
    {
        var configPath = Path.Combine(basePath, "dydo.json");
        if (!File.Exists(configPath))
            return new Dictionary<string, List<string>> { ["source"] = ["src/**"], ["tests"] = ["tests/**"] };

        try
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize(json, DydoConfigJsonContext.Default.DydoConfig);
            return new RoleDefinitionService().ResolvePathSets(config);
        }
        catch
        {
            return new Dictionary<string, List<string>> { ["source"] = ["src/**"], ["tests"] = ["tests/**"] };
        }
    }
}
