namespace DynaDocs.Services;

using DynaDocs.Models;
using DynaDocs.Utils;

/// <summary>
/// Reads the shipped skill templates — the template IS the metadata, so there is nothing else
/// to consult.
/// </summary>
public static class SkillTemplateService
{
    /// <summary>
    /// Enumerates every shipped skill template. Metadata comes from the template frontmatter:
    /// <c>name</c>, <c>description</c>, <c>emit</c> (agent+skill unless <c>skill</c>),
    /// <c>read-only</c>, <c>delegates</c>, <c>invocation</c>.
    ///
    /// The shipped set already excludes retired names, so sync's retired-artifact sweep is
    /// never suppressed by a source dydo still carries through a transition.
    /// </summary>
    public static List<SkillTemplate> DiscoverSkills()
    {
        return TemplateGenerator.GetBuiltInSkillTemplateNames()
            .Select(templateFile =>
                Parse(templateFile, TemplateGenerator.ReadBuiltInTemplate(templateFile)))
            .ToList();
    }

    /// <summary>
    /// Turns one skill template's source into its <see cref="SkillTemplate"/>. Throws
    /// <see cref="InvalidDataException"/> naming the file when <c>name</c> is missing or differs
    /// from the filename slug, or when <c>invocation</c> is neither <c>automatic</c> nor
    /// <c>explicit</c>.
    /// </summary>
    public static SkillTemplate Parse(string templateFile, string content)
    {
        var fields = FrontmatterParser.ParseFields(content) ?? [];
        var slug = templateFile["skill-".Length..^".template.md".Length];

        if (!fields.TryGetValue("name", out var declaredName))
        {
            throw new InvalidDataException(
                $"Skill template '{templateFile}' has no 'name:'; expected 'name: {slug}'.");
        }

        if (!declaredName.Equals(slug, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Skill template '{templateFile}' declares 'name: {declaredName}'; "
                + $"expected '{slug}' from the filename.");
        }

        return new SkillTemplate
        {
            Name = slug,
            TemplateFile = templateFile,
            Description = fields.TryGetValue("description", out var d) ? d : "",
            EmitAgent = !fields.TryGetValue("emit", out var e)
                || e.Equals("agent", StringComparison.OrdinalIgnoreCase),
            ReadOnly = fields.TryGetValue("read-only", out var r)
                && r.Equals("true", StringComparison.OrdinalIgnoreCase),
            Delegates = fields.TryGetValue("delegates", out var g)
                && g.Equals("true", StringComparison.OrdinalIgnoreCase),
            ExplicitInvocation = ParseExplicitInvocation(fields, templateFile),
        };
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
}
