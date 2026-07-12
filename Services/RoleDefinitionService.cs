namespace DynaDocs.Services;

using System.Text.Json;
using DynaDocs.Models;
using DynaDocs.Serialization;

public class RoleDefinitionService : IRoleDefinitionService
{
    /// <summary>
    /// Roles that <c>dydo sync</c> compiles into a skill but that are NOT claimable Tier-1
    /// identities (Decision 024): they appear in <see cref="GetBaseRoleDefinitions"/> only to
    /// drive skill generation, and are excluded from the on-disk role roster written by
    /// <see cref="WriteBaseRoleDefinitions"/> (which feeds the guard's claimable-role set).
    /// </summary>
    public static readonly HashSet<string> SkillOnlyRoles = new(StringComparer.OrdinalIgnoreCase) { "planner" };

    /// <summary>
    /// Roles that exist only as workflow-spawned agent-types (Decision 026): <c>dydo sync</c>
    /// compiles them into a native agent + skill like the worker roles, but they are never
    /// claimable Tier-1 identities, so — like <see cref="SkillOnlyRoles"/> — they are excluded
    /// from the on-disk role roster and the claimable/mode surfaces.
    /// </summary>
    public static readonly HashSet<string> WorkflowOnlyRoles = new(StringComparer.OrdinalIgnoreCase) { "sprint-auditor" };

    /// <summary>
    /// The union every claimable-surface filter uses (roster, registry fallback, role table,
    /// mode names) — a single set so those filters can never drift apart.
    /// </summary>
    public static readonly HashSet<string> NonClaimableRoles =
        new(SkillOnlyRoles.Concat(WorkflowOnlyRoles), StringComparer.OrdinalIgnoreCase);

    public static List<RoleDefinition> GetBaseRoleDefinitions()
    {
        return
        [
            new RoleDefinition
            {
                Name = "code-writer",
                Description = "Implements features and fixes bugs in source code.",
                Base = true,
                WritablePaths = ["{source}", "{tests}", "dydo/agents/{self}/**", "dydo/project/backlog/**"],
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
                        Message = "Cannot release: dispatched code-writers must dispatch a reviewer before releasing.\n  dydo dispatch --auto-close --role reviewer --task {task} --brief \"Review changes for {task}\""
                    },
                    new RoleConstraint
                    {
                        Type = "requires-commit",
                        Message = "Code-writers in worktrees must commit before releasing."
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
                        RequiredRoles = ["code-writer", "test-writer"],
                        OnlyWhenDispatched = true,
                        Message = "Reviewers can only dispatch a code-writer when dispatched by a code-writer or test-writer. Report findings back to your dispatcher instead.\n  dydo msg --to {dispatcher} --subject {task} --body \"Review findings: ...\""
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
                Name = "sprint-auditor",
                Description = "Audits an entire merged sprint as one unit, hunting real cross-slice issues and returning a strict verdict with findings.",
                Base = true,
                WritablePaths = ["dydo/agents/{self}/**"],
                ReadOnlyPaths = ["**"],
                TemplateFile = "mode-sprint-auditor.template.md",
                DenialHint = "Sprint-auditor role can only edit own workspace.",
                Constraints = []
            },
            new RoleDefinition
            {
                Name = "co-thinker",
                Description = "Collaborates on design decisions and architecture.",
                Base = true,
                WritablePaths = ["dydo/agents/{self}/**", "dydo/project/decisions/**", "dydo/project/issues/**", "dydo/project/backlog/**"],
                ReadOnlyPaths = ["{source}", "{tests}"],
                TemplateFile = "mode-co-thinker.template.md",
                DenialHint = "Co-thinker role can edit own workspace and decisions.",
                Constraints = []
            },
            new RoleDefinition
            {
                Name = "chief-of-staff",
                Description = "The human's right hand — triages the backlog and idea funnel, routes work to domain orchestrators, reports status, and mediates between agents.",
                Base = true,
                WritablePaths = ["dydo/agents/{self}/**", "dydo/project/tasks/**", "dydo/project/decisions/**", "dydo/project/issues/**", "dydo/project/backlog/**"],
                ReadOnlyPaths = ["**"],
                TemplateFile = "mode-chief-of-staff.template.md",
                DenialHint = "Chief-of-staff writes PM objects and docs, never code.",
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
                WritablePaths = ["dydo/agents/{self}/**", "dydo/project/tasks/**", "dydo/project/decisions/**", "dydo/project/issues/**", "dydo/project/backlog/**"],
                ReadOnlyPaths = ["**"],
                TemplateFile = "mode-orchestrator.template.md",
                CanOrchestrate = true,
                Constraints =
                [
                    new RoleConstraint
                    {
                        Type = "requires-prior",
                        RequiredRoles = ["co-thinker"],
                        Message = "You are a {current_role}. Orchestrator requires prior co-thinker experience on this task. Ask the user for clarification."
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
                ValidateRoleTransition(constraint, errors);
                break;
            case "requires-prior":
                ValidateRequiresPrior(constraint, errors);
                break;
            case "panel-limit":
                ValidatePanelLimit(constraint, errors);
                break;
            case "requires-dispatch":
                ValidateRequiresDispatch(constraint, errors);
                break;
            case "requires-commit":
                break;
            case "dispatch-restriction":
                ValidateDispatchRestriction(constraint, errors);
                break;
            default:
                errors.Add($"Unknown constraint type: '{constraint.Type}'.");
                break;
        }
    }

    private static void ValidateRoleTransition(RoleConstraint constraint, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(constraint.FromRole))
            errors.Add("Constraint 'role-transition' requires 'fromRole'.");
    }

    private static void ValidateRequiresPrior(RoleConstraint constraint, List<string> errors)
    {
        if (constraint.RequiredRoles == null || constraint.RequiredRoles.Count == 0)
            errors.Add("Constraint 'requires-prior' requires 'requiredRoles'.");
    }

    private static void ValidatePanelLimit(RoleConstraint constraint, List<string> errors)
    {
        if (constraint.MaxCount == null || constraint.MaxCount < 1)
            errors.Add("Constraint 'panel-limit' requires 'maxCount' >= 1.");
    }

    private static void ValidateRequiresDispatch(RoleConstraint constraint, List<string> errors)
    {
        if (constraint.RequiredRoles == null || constraint.RequiredRoles.Count == 0)
            errors.Add("Constraint 'requires-dispatch' requires 'requiredRoles'.");
    }

    private static void ValidateDispatchRestriction(RoleConstraint constraint, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(constraint.TargetRole))
            errors.Add("Constraint 'dispatch-restriction' requires 'targetRole'.");
        if (constraint.RequiredRoles == null || constraint.RequiredRoles.Count == 0)
            errors.Add("Constraint 'dispatch-restriction' requires 'requiredRoles'.");
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

        foreach (var role in GetBaseRoleDefinitions().Where(r => !NonClaimableRoles.Contains(r.Name)))
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
