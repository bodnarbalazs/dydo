namespace DynaDocs.Services;

using DynaDocs.Models;
using DynaDocs.Utils;

/// <summary>
/// Discovers roles from mode templates (the template IS the role — its frontmatter carries
/// the metadata) and resolves the {source}/{tests} path sets used by tool-scoped nudges.
/// </summary>
public class RoleDefinitionService : IRoleDefinitionService
{
    /// <summary>
    /// Transitional metadata for the shipped base roles, used only while their mode
    /// templates do not yet carry <c>description</c>/<c>emit</c>/<c>read-only</c>
    /// frontmatter (those blocks land with the prompt-template pass). Frontmatter always
    /// wins; delete this table once every shipped template carries the keys.
    /// </summary>
    private static readonly Dictionary<string, (string Description, bool EmitAgent, bool ReadOnly)> BaseRoleSeed =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["code-writer"] = ("Implements features and fixes bugs in source code.", true, false),
            ["reviewer"] = ("Reviews code changes for quality and correctness.", true, true),
            ["test-writer"] = ("Writes and maintains test suites.", true, false),
            ["docs-writer"] = ("Creates and maintains documentation.", true, false),
            ["planner"] = ("Creates implementation plans and task breakdowns.", false, false),
            ["orchestrator"] = ("Coordinates multi-agent workflows and task dispatch.", false, false),
            ["co-thinker"] = ("Collaborates on design decisions and architecture.", false, false),
            ["chief-of-staff"] = ("The human's right hand — triages the backlog and idea funnel, routes work to domain orchestrators, reports status, and mediates between agents.", false, false),
        };

    /// <summary>
    /// Enumerates every role: the shipped mode templates plus any project-local
    /// <c>dydo/_system/templates/mode-*.template.md</c> — which is how a custom role
    /// compiles: drop a mode template in, run <c>dydo sync</c>. Metadata comes from the
    /// template frontmatter (<c>description</c>, <c>emit</c>, <c>read-only</c>), falling
    /// back to <see cref="BaseRoleSeed"/> for shipped roles whose templates predate the keys.
    /// </summary>
    public static List<RoleDefinition> DiscoverRoles(string? projectRoot = null)
    {
        var templateNames = new SortedSet<string>(
            TemplateGenerator.GetBuiltInModeTemplateNames(), StringComparer.OrdinalIgnoreCase);
        templateNames.UnionWith(TemplateGenerator.GetProjectModeTemplateNames(projectRoot));

        var roles = new List<RoleDefinition>();
        foreach (var templateFile in templateNames)
        {
            var name = templateFile["mode-".Length..^".template.md".Length];
            var fields = FrontmatterParser.ParseFields(
                TemplateGenerator.ReadTemplate(templateFile, projectRoot)) ?? [];
            var seed = BaseRoleSeed.TryGetValue(name, out var s)
                ? s
                : (Description: "", EmitAgent: true, ReadOnly: false);

            roles.Add(new RoleDefinition
            {
                Name = name,
                TemplateFile = templateFile,
                Description = fields.TryGetValue("description", out var d) && d.Length > 0
                    ? d
                    : seed.Description,
                EmitAgent = fields.TryGetValue("emit", out var e) && e.Length > 0
                    ? e.Equals("agent", StringComparison.OrdinalIgnoreCase)
                    : seed.EmitAgent,
                ReadOnly = fields.TryGetValue("read-only", out var r)
                    ? r.Equals("true", StringComparison.OrdinalIgnoreCase)
                    : seed.ReadOnly,
            });
        }

        return roles;
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
}
