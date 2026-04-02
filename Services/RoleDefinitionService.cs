namespace DynaDocs.Services;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;

public class RoleDefinitionService : IRoleDefinitionService
{
    public static List<RoleDefinition> GetBaseRoleDefinitions()
    {
        return
        [
            new RoleDefinition
            {
                Name = "code-writer",
                Description = "Implements features and fixes bugs in source code.",
                Base = true,
                WritablePaths = ["{source}", "{tests}", "dydo/agents/{self}/**"],
                ReadOnlyPaths = ["dydo/**", "project/**"],
                TemplateFile = "mode-code-writer.template.md",
                DenialHint = "Code-writer role can only edit configured source/test paths and own workspace.",
                Constraints =
                [
                    new RoleConstraint
                    {
                        Type = "requires-dispatch",
                        RequiredRoles = ["reviewer"],
                        OnlyWhenDispatched = true,
                        Message = "Cannot release: dispatched code-writers must dispatch a reviewer before releasing.\n  dydo dispatch --no-wait --auto-close --role reviewer --task {task} --brief \"Review changes for {task}\""
                    }
                ],
                ConditionalMustReads =
                [
                    new ConditionalMustRead
                    {
                        When = new ConditionalMustReadCondition { MarkerExists = ".merge-source" },
                        Path = "dydo/guides/how-to-merge-worktrees.md"
                    }
                ]
            },
            new RoleDefinition
            {
                Name = "reviewer",
                Description = "Reviews code changes for quality and correctness.",
                Base = true,
                WritablePaths = ["dydo/agents/{self}/**"],
                ReadOnlyPaths = ["**"],
                TemplateFile = "mode-reviewer.template.md",
                DenialHint = "Reviewer role can only edit own workspace.",
                Constraints =
                [
                    new RoleConstraint
                    {
                        Type = "role-transition",
                        FromRole = "code-writer",
                        Message = "Agent {agent} was code-writer on task '{task}' and cannot be reviewer on the same task. Dispatch to a different agent for review."
                    },
                    new RoleConstraint
                    {
                        Type = "dispatch-restriction",
                        TargetRole = "code-writer",
                        RequiredRoles = ["code-writer"],
                        OnlyWhenDispatched = true,
                        Message = "Reviewers can only dispatch a code-writer when dispatched by a code-writer. Report findings back to your dispatcher instead.\n  dydo msg --to {dispatcher} --subject {task} --body \"Review findings: ...\""
                    }
                ],
                ConditionalMustReads =
                [
                    new ConditionalMustRead
                    {
                        When = new ConditionalMustReadCondition { TaskNameMatches = "*-merge" },
                        Path = "dydo/guides/how-to-review-worktree-merges.md"
                    },
                    new ConditionalMustRead
                    {
                        Path = "dydo/project/tasks/{task}.md"
                    },
                    new ConditionalMustRead
                    {
                        When = new ConditionalMustReadCondition { DispatchedByRole = "docs-writer" },
                        Path = "dydo/reference/writing-docs.md"
                    }
                ]
            },
            new RoleDefinition
            {
                Name = "co-thinker",
                Description = "Collaborates on design decisions and architecture.",
                Base = true,
                WritablePaths = ["dydo/agents/{self}/**", "dydo/project/decisions/**"],
                ReadOnlyPaths = ["{source}", "{tests}"],
                TemplateFile = "mode-co-thinker.template.md",
                DenialHint = "Co-thinker role can edit own workspace and decisions.",
                Constraints = []
            },
            new RoleDefinition
            {
                Name = "docs-writer",
                Description = "Creates and maintains documentation.",
                Base = true,
                WritablePaths = ["dydo/understand/**", "dydo/guides/**", "dydo/reference/**", "dydo/project/**", "dydo/_system/**", "dydo/_assets/**", "dydo/*.md", "dydo/agents/{self}/**"],
                ReadOnlyPaths = ["{source}", "{tests}"],
                TemplateFile = "mode-docs-writer.template.md",
                DenialHint = "Docs-writer role can only edit dydo/** (except other agents' workspaces) and own workspace.",
                Constraints = []
            },
            new RoleDefinition
            {
                Name = "planner",
                Description = "Creates implementation plans and task breakdowns.",
                Base = true,
                WritablePaths = ["dydo/agents/{self}/**", "dydo/project/tasks/**"],
                ReadOnlyPaths = ["{source}"],
                TemplateFile = "mode-planner.template.md",
                DenialHint = "Planner role can only edit own workspace and tasks.",
                Constraints = []
            },
            new RoleDefinition
            {
                Name = "test-writer",
                Description = "Writes and maintains test suites.",
                Base = true,
                WritablePaths = ["dydo/agents/{self}/**", "{tests}", "dydo/project/pitfalls/**"],
                ReadOnlyPaths = ["{source}"],
                TemplateFile = "mode-test-writer.template.md",
                DenialHint = "Test-writer role can edit own workspace, tests, and pitfalls.",
                Constraints = []
            },
            new RoleDefinition
            {
                Name = "orchestrator",
                Description = "Coordinates multi-agent workflows and task dispatch.",
                Base = true,
                WritablePaths = ["dydo/agents/{self}/**", "dydo/project/tasks/**", "dydo/project/decisions/**"],
                ReadOnlyPaths = ["**"],
                TemplateFile = "mode-orchestrator.template.md",
                CanOrchestrate = true,
                Constraints =
                [
                    new RoleConstraint
                    {
                        Type = "requires-prior",
                        RequiredRoles = ["co-thinker", "planner"],
                        Message = "You are a {current_role}. Orchestrator requires prior co-thinker or planner experience on this task. Ask the user for clarification."
                    }
                ]
            },
            new RoleDefinition
            {
                Name = "inquisitor",
                Description = "Conducts documentation and knowledge audits.",
                Base = true,
                WritablePaths = ["dydo/agents/{self}/**", "dydo/project/inquisitions/**"],
                ReadOnlyPaths = ["{source}", "{tests}"],
                TemplateFile = "mode-inquisitor.template.md",
                CanOrchestrate = true,
                Constraints =
                [
                    new RoleConstraint
                    {
                        Type = "requires-dispatch",
                        RequiredRoles = ["judge", "inquisitor"],
                        RequireAll = false,
                        OnlyWhenDispatched = true,
                        Message = "Cannot release: dispatched inquisitors must dispatch a judge or another inquisitor before releasing.\n  dydo dispatch --no-wait --auto-close --role judge --task {task} --brief \"Judge findings for {task}\""
                    }
                ]
            },
            new RoleDefinition
            {
                Name = "judge",
                Description = "Evaluates inquisition reports and arbitrates disputes.",
                Base = true,
                WritablePaths = ["dydo/agents/{self}/**", "dydo/project/issues/**"],
                ReadOnlyPaths = ["{source}", "{tests}"],
                TemplateFile = "mode-judge.template.md",
                CanOrchestrate = true,
                Constraints =
                [
                    new RoleConstraint
                    {
                        Type = "panel-limit",
                        MaxCount = 3,
                        Message = "Maximum 3 judges already active on task '{task}'. Escalate to the human."
                    }
                ]
            }
        ];
    }

    public List<RoleDefinition> LoadRoleDefinitions(string basePath)
    {
        var rolesDir = Path.Combine(basePath, "dydo", "_system", "roles");
        if (!Directory.Exists(rolesDir))
            return [];

        var files = Directory.GetFiles(rolesDir, "*.role.json");
        if (files.Length == 0)
            return [];

        var roles = new List<RoleDefinition>();
        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var role = JsonSerializer.Deserialize(json, DydoConfigJsonContext.Default.RoleDefinition);
            if (role != null)
                roles.Add(role);
        }

        return roles;
    }

    public Dictionary<string, (List<string> Writable, List<string> ReadOnly)> BuildPermissionMap(
        List<RoleDefinition> roles, Dictionary<string, List<string>> pathSets)
    {
        var result = new Dictionary<string, (List<string> Writable, List<string> ReadOnly)>();

        foreach (var role in roles)
        {
            var writable = ExpandPathSets(role.WritablePaths, pathSets);
            var readOnly = ExpandPathSets(role.ReadOnlyPaths, pathSets);
            result[role.Name] = (writable, readOnly);
        }

        return result;
    }

    public Dictionary<string, List<string>> ResolvePathSets(DydoConfig? config)
    {
        if (config?.Paths.PathSets != null)
            return config.Paths.PathSets;

        return new Dictionary<string, List<string>>
        {
            ["source"] = config?.Paths.Source ?? ["src/**"],
            ["tests"] = config?.Paths.Tests ?? ["tests/**"]
        };
    }

    public bool ValidateRoleDefinition(RoleDefinition role, out List<string> errors)
    {
        errors = [];

        if (string.IsNullOrWhiteSpace(role.Name))
            errors.Add("Role name is required.");
        if (string.IsNullOrWhiteSpace(role.Description))
            errors.Add("Role description is required.");
        if (role.WritablePaths.Count == 0)
            errors.Add("At least one writable path is required.");
        if (string.IsNullOrWhiteSpace(role.TemplateFile))
            errors.Add("Template file is required.");

        foreach (var constraint in role.Constraints)
            ValidateConstraint(constraint, errors);

        foreach (var cmr in role.ConditionalMustReads ?? [])
            ValidateConditionalMustRead(cmr, errors);

        return errors.Count == 0;
    }

    private static void ValidateConstraint(RoleConstraint constraint, List<string> errors)
    {
        switch (constraint.Type)
        {
            case "role-transition":
                if (string.IsNullOrWhiteSpace(constraint.FromRole))
                    errors.Add("Constraint 'role-transition' requires 'fromRole'.");
                break;
            case "requires-prior":
                if (constraint.RequiredRoles == null || constraint.RequiredRoles.Count == 0)
                    errors.Add("Constraint 'requires-prior' requires 'requiredRoles'.");
                break;
            case "panel-limit":
                if (constraint.MaxCount == null || constraint.MaxCount < 1)
                    errors.Add("Constraint 'panel-limit' requires 'maxCount' >= 1.");
                break;
            case "requires-dispatch":
                if (constraint.RequiredRoles == null || constraint.RequiredRoles.Count == 0)
                    errors.Add("Constraint 'requires-dispatch' requires 'requiredRoles'.");
                break;
            case "dispatch-restriction":
                if (string.IsNullOrWhiteSpace(constraint.TargetRole))
                    errors.Add("Constraint 'dispatch-restriction' requires 'targetRole'.");
                if (constraint.RequiredRoles == null || constraint.RequiredRoles.Count == 0)
                    errors.Add("Constraint 'dispatch-restriction' requires 'requiredRoles'.");
                break;
            default:
                errors.Add($"Unknown constraint type: '{constraint.Type}'.");
                break;
        }
    }

    private static void ValidateConditionalMustRead(ConditionalMustRead cmr, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(cmr.Path))
            errors.Add("Conditional must-read 'path' is required.");

        if (cmr.When != null)
        {
            var hasAny = cmr.When.MarkerExists != null
                || cmr.When.TaskNameMatches != null
                || cmr.When.DispatchedByRole != null;
            if (!hasAny)
                errors.Add("Conditional must-read 'when' must have at least one condition.");

            if (cmr.When.MarkerExists != null
                && (cmr.When.MarkerExists.Contains('/') || cmr.When.MarkerExists.Contains('\\')))
                errors.Add("Conditional must-read 'markerExists' must be a filename, not a path.");

            if (cmr.When.TaskNameMatches != null && string.IsNullOrWhiteSpace(cmr.When.TaskNameMatches))
                errors.Add("Conditional must-read 'taskNameMatches' must not be empty.");

            if (cmr.When.DispatchedByRole != null && string.IsNullOrWhiteSpace(cmr.When.DispatchedByRole))
                errors.Add("Conditional must-read 'dispatchedByRole' must not be empty.");
        }
    }

    public void WriteBaseRoleDefinitions(string basePath)
    {
        var rolesDir = Path.Combine(basePath, "dydo", "_system", "roles");
        Directory.CreateDirectory(rolesDir);

        foreach (var role in GetBaseRoleDefinitions())
        {
            var filePath = Path.Combine(rolesDir, $"{role.Name}.role.json");
            var json = JsonSerializer.Serialize(role, DydoConfigJsonContext.Default.RoleDefinition);
            File.WriteAllText(filePath, json);
        }
    }

    private static List<string> ExpandPathSets(List<string> paths, Dictionary<string, List<string>> pathSets)
    {
        var result = new List<string>();

        foreach (var path in paths)
        {
            if (path.StartsWith('{') && path.EndsWith('}'))
            {
                var setName = path[1..^1];
                if (pathSets.TryGetValue(setName, out var setPaths))
                    result.AddRange(setPaths);
                else
                    result.Add(path); // unresolved — leave as-is
            }
            else
            {
                result.Add(path);
            }
        }

        return result;
    }
}
