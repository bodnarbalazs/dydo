namespace DynaDocs.Services;

using DynaDocs.Models;
using DynaDocs.Utils;

/// <summary>
/// Discovers roles from skill templates (the template IS the role — its frontmatter carries
/// the metadata) and resolves the {source}/{tests} path sets used by tool-scoped nudges.
/// </summary>
public class RoleDefinitionService : IRoleDefinitionService
{
    /// <summary>
    /// Enumerates every role: the shipped skill templates plus any project-local
    /// <c>dydo/_system/templates/skill-*.template.md</c> — which is how a custom role
    /// compiles: drop a skill template in, run <c>dydo sync</c>. Metadata comes from the
    /// template frontmatter: <c>description</c>, <c>emit</c> (agent+skill unless <c>skill</c>),
    /// <c>read-only</c>, <c>delegates</c>, <c>invocation</c>.
    ///
    /// The shipped set already excludes retired names, so sync's retired-artifact sweep is
    /// never suppressed by a source dydo still carries through a transition. A project-local
    /// template of a retired name still defines the role — that is the deliberate escape hatch.
    /// </summary>
    public static List<RoleDefinition> DiscoverRoles(string? projectRoot = null)
    {
        var templateNames = new SortedSet<string>(
            TemplateGenerator.GetBuiltInSkillTemplateNames(), StringComparer.OrdinalIgnoreCase);
        templateNames.UnionWith(TemplateGenerator.GetProjectSkillTemplateNames(projectRoot));

        var roles = new List<RoleDefinition>();
        foreach (var templateFile in templateNames)
        {
            var name = templateFile["skill-".Length..^".template.md".Length];
            var fields = FrontmatterParser.ParseFields(
                TemplateGenerator.ReadTemplate(templateFile, projectRoot)) ?? [];
            var explicitInvocation = ParseExplicitInvocation(fields, templateFile);

            roles.Add(new RoleDefinition
            {
                Name = name,
                TemplateFile = templateFile,
                Description = fields.TryGetValue("description", out var d) ? d : "",
                EmitAgent = !fields.TryGetValue("emit", out var e)
                    || e.Equals("agent", StringComparison.OrdinalIgnoreCase),
                ReadOnly = fields.TryGetValue("read-only", out var r)
                    && r.Equals("true", StringComparison.OrdinalIgnoreCase),
                Delegates = fields.TryGetValue("delegates", out var g)
                    && g.Equals("true", StringComparison.OrdinalIgnoreCase),
                ExplicitInvocation = explicitInvocation,
            });
        }

        return roles;
    }

    private static bool ParseExplicitInvocation(
        IReadOnlyDictionary<string, string> fields,
        string templateFile)
    {
        if (!fields.TryGetValue("invocation", out var value)
            || value.Equals("automatic", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (value.Equals("explicit", StringComparison.OrdinalIgnoreCase))
            return true;

        throw new InvalidDataException(
            $"Skill template '{templateFile}' has invalid invocation '{value}'; "
            + "expected 'automatic' or 'explicit'.");
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
