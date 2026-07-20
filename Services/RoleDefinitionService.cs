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
    /// Enumerates every role: the shipped mode templates plus any project-local
    /// <c>dydo/_system/templates/mode-*.template.md</c> — which is how a custom role
    /// compiles: drop a mode template in, run <c>dydo sync</c>. Metadata comes from the
    /// template frontmatter: <c>description</c>, <c>emit</c> (agent+skill unless <c>skill</c>),
    /// <c>read-only</c>.
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

            roles.Add(new RoleDefinition
            {
                Name = name,
                TemplateFile = templateFile,
                Description = fields.TryGetValue("description", out var d) ? d : "",
                EmitAgent = !fields.TryGetValue("emit", out var e)
                    || e.Equals("agent", StringComparison.OrdinalIgnoreCase),
                ReadOnly = fields.TryGetValue("read-only", out var r)
                    && r.Equals("true", StringComparison.OrdinalIgnoreCase),
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
