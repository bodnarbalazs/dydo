namespace DynaDocs.Services;

using DynaDocs.Models;
using DynaDocs.Utils;
using System.Text.RegularExpressions;

/// <summary>
/// Reads the shipped skill templates — the template IS the metadata, so there is nothing else
/// to consult.
/// </summary>
public static partial class SkillTemplateService
{
    private static readonly HashSet<string> AllowedFields = new(
        ["name", "description", "emit", "read-only", "delegates", "invocation", "web", "argument-hint"],
        StringComparer.Ordinal);
    /// <summary>
    /// Enumerates every shipped skill template. Metadata comes from the template frontmatter:
    /// <c>name</c>, <c>description</c>, <c>emit</c> (agent+skill unless <c>skill</c>),
    /// <c>read-only</c>, <c>delegates</c>, <c>invocation</c>, <c>web</c>, <c>argument-hint</c>.
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

    public static List<SkillTemplate> DiscoverLocalCatalog(string projectRoot, DydoConfig config)
    {
        var sourceRoot = GetSourceRoot(projectRoot, config);
        if (!Directory.Exists(sourceRoot))
            throw new InvalidDataException($"Local template source directory is missing: {sourceRoot}. Run 'dydo template update'.");

        var errors = new List<string>();
        var files = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal);

        var skillFiles = new Dictionary<string, string>(StringComparer.Ordinal);
        var resourceFiles = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var allNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in files)
        {
            var file = Path.GetFileName(path);
            if (!file.EndsWith(".template.md", StringComparison.Ordinal))
                continue;
            var isSkill = file.StartsWith("skill-", StringComparison.Ordinal);
            var isResource = file.StartsWith("resource-", StringComparison.Ordinal);
            if (!isSkill && !isResource)
                continue;
            if (isResource && !file["resource-".Length..^".template.md".Length]
                    .Contains("-resource-", StringComparison.Ordinal))
                continue;
            if (Path.GetDirectoryName(path) != sourceRoot)
            {
                errors.Add($"'{Path.GetRelativePath(sourceRoot, path).Replace('\\', '/')}' is nested; local templates must be top-level.");
                continue;
            }
            if (isSkill)
            {
                var slug = file["skill-".Length..^".template.md".Length];
                if (!ConfigService.IsValidSlug(slug))
                {
                    // A historic resource owner may itself begin with `skill-`.  Its exact
                    // recorded resource provenance settles that finite legacy spelling before
                    // the modern skill grammar gets a chance to reject the filename.
                    if (!HasRecordedLegacyResource(file, config))
                        errors.Add($"'{file}' has an invalid skill name or protected -resource- delimiter.");
                    continue;
                }
                if (!allNames.Add(slug))
                    errors.Add($"'{file}' collides ordinal-ignore-case with another skill source.");
                else
                    skillFiles[slug] = path;
                continue;
            }

            var remainder = file["resource-".Length..^".template.md".Length];
            var delimiter = remainder.IndexOf("-resource-", StringComparison.Ordinal);
            if (delimiter < 0)
            {
                errors.Add($"'{file}' has an invalid skill or resource name.");
                continue;
            }
            var skillName = remainder[..delimiter];
            var resourceName = remainder[(delimiter + "-resource-".Length)..];
            if (!ConfigService.IsValidSlug(skillName) || !ConfigService.IsValidSlug(resourceName))
            {
                errors.Add($"'{file}' has an invalid skill or resource name.");
                continue;
            }
            var resources = resourceFiles.GetValueOrDefault(skillName);
            if (resources == null)
            {
                resources = new Dictionary<string, string>(StringComparer.Ordinal);
                resourceFiles[skillName] = resources;
            }
            if (resources.Keys.Any(name => name.Equals(resourceName, StringComparison.OrdinalIgnoreCase)))
                errors.Add($"'{file}' collides ordinal-ignore-case with another resource source.");
            else
                resources[resourceName] = path;
        }

        errors.AddRange(FindLegacyResourceDiagnostics(files, sourceRoot, projectRoot, skillFiles, config));

        foreach (var (skillName, resources) in resourceFiles)
        {
            if (!skillFiles.ContainsKey(skillName))
                errors.AddRange(resources.Values.Select(path => $"'{Path.GetFileName(path)}' has no matching skill source."));
        }

        var shippedSkills = TemplateGenerator.GetBuiltInSkillTemplateNames()
            .Select(file => file["skill-".Length..^".template.md".Length])
            .ToHashSet(StringComparer.Ordinal);
        var planned = config.Skills.ToDictionary(
            entry => entry.Key,
            entry => Clone(entry.Value),
            StringComparer.Ordinal);
        var catalog = new List<SkillTemplate>();

        foreach (var (name, path) in skillFiles)
        {
            var existing = planned.GetValueOrDefault(name);
            var shipped = shippedSkills.Contains(name);
            if (shipped && existing?.Origin == "custom")
            {
                errors.Add($"'{Path.GetFileName(path)}' collides with a newly shipped skill named '{name}'.");
                continue;
            }
            if (!shipped && existing?.Origin == "shipped")
            {
                errors.Add($"'{Path.GetFileName(path)}' collides with the retired shipped name '{name}'.");
                continue;
            }

            SkillTemplate? skill = null;
            try
            {
                var content = File.ReadAllText(path);
                ValidateTemplate(path, content, projectRoot);
                skill = Parse(Path.GetFileName(path), content);
            }
            catch (InvalidDataException ex)
            {
                errors.Add(ex.Message);
            }
            if (skill == null)
                continue;

            var resources = resourceFiles.GetValueOrDefault(name) ?? new Dictionary<string, string>();
            var embeddedResources = TemplateGenerator.GetSkillResourceTemplateNames(name)
                .Select(file => file[$"resource-{name}-resource-".Length..^".template.md".Length])
                .ToHashSet(StringComparer.Ordinal);
            if (shipped)
            {
                foreach (var resource in resources.Keys.Where(resource => !embeddedResources.Contains(resource)))
                    errors.Add($"'resource-{name}-resource-{resource}.template.md' adds a resource to shipped skill '{name}'.");
            }

            var contentWithIncludes = ResolveIncludesStrict(File.ReadAllText(path), projectRoot, path);
            var references = ResourceLinkRegex().Matches(contentWithIncludes)
                .Select(match => match.Groups[1].Value)
                .ToHashSet(StringComparer.Ordinal);
            foreach (var reference in references.Where(reference => !resources.ContainsKey(reference)))
                errors.Add($"'{Path.GetFileName(path)}' references missing resource '{reference}'.");
            foreach (var resource in resources.Keys.Where(resource => !references.Contains(resource)))
                errors.Add($"'{Path.GetFileName(resources[resource])}' is not referenced by skill '{name}'.");

            existing ??= new SkillSwitchConfig { Enabled = true };
            existing.Origin = shipped ? "shipped" : "custom";
            existing.EmitAgent = skill.EmitAgent;
            existing.CodexMetadata = skill.ExplicitInvocation || skill.ArgumentHint != null;
            existing.Resources = resources.Keys.OrderBy(resource => resource, StringComparer.Ordinal).ToList();
            planned[name] = existing;
            catalog.Add(skill);
        }

        foreach (var (name, entry) in planned.Where(entry => !skillFiles.ContainsKey(entry.Key)))
        {
            if (entry.Origin == null || entry.EmitAgent == null || entry.CodexMetadata == null || entry.Resources == null)
                errors.Add($"Skill switch '{name}' has no source and no complete prior generated provenance.");
        }

        if (errors.Count > 0)
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));

        config.Skills = planned
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal);
        return catalog.OrderBy(skill => skill.Name, StringComparer.Ordinal).ToList();
    }

    internal static string ReadSource(SkillTemplate skill, string projectRoot)
    {
        var config = new ConfigService().LoadConfigStrict(projectRoot)
            ?? throw new FileNotFoundException("Local dydo.json is missing; skill emission requires the local catalog.");
        return File.ReadAllText(Path.Combine(GetSourceRoot(projectRoot, config), skill.TemplateFile));
    }

    internal static IEnumerable<(string FileName, string Content)> ReadResources(
        SkillTemplate skill, string projectRoot)
    {
        var config = new ConfigService().LoadConfigStrict(projectRoot)
            ?? throw new FileNotFoundException("Local dydo.json is missing; resource emission requires the local catalog.");
        var root = GetSourceRoot(projectRoot, config);
        var content = ResolveIncludesStrict(ReadSource(skill, projectRoot), projectRoot, skill.TemplateFile);
        var resources = ResourceLinkRegex().Matches(content)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);
        foreach (var name in resources)
        {
            var path = Path.Combine(root, $"resource-{skill.Name}-resource-{name}.template.md");
            yield return ($"{name}.md", File.ReadAllText(path));
        }
    }

    private static string GetSourceRoot(string projectRoot, DydoConfig config) =>
        Path.Combine(projectRoot, config.Structure.Root, "_system", "templates");

    private static IEnumerable<string> FindLegacyResourceDiagnostics(
        IEnumerable<string> files,
        string sourceRoot,
        string projectRoot,
        IReadOnlyDictionary<string, string> skillFiles,
        DydoConfig config)
    {
        foreach (var path in files)
        {
            var file = Path.GetFileName(path);
            if (!file.EndsWith(".template.md", StringComparison.Ordinal)
                || !TryParseLegacyResource(file, out var owner, out var resource))
                continue;

            // `skill-resource-guide` is a valid canonical skill named `resource-guide`.
            // Its old-looking suffix never changes that structural identity, even after the
            // separate skill's resource provenance has been recorded on a prior pass.
            if (file.StartsWith("skill-", StringComparison.Ordinal)
                && ConfigService.IsValidSlug(file["skill-".Length..^".template.md".Length]))
                continue;

            var canonical = $"resource-{owner}-resource-{resource}.template.md";
            var canonicalPath = Path.Combine(sourceRoot, canonical);
            if (file.StartsWith("resource-", StringComparison.Ordinal) && File.Exists(canonicalPath))
                continue;
            var hasRecordedResource = config.Skills.TryGetValue(owner, out var switchEntry)
                && switchEntry.Resources?.Contains(resource, StringComparer.Ordinal) == true;
            var hasMissingReference = !File.Exists(canonicalPath)
                && skillFiles.TryGetValue(owner, out var ownerPath)
                && HasValidResourceReference(ownerPath, projectRoot, resource);
            if (!hasRecordedResource && !hasMissingReference)
                continue;

            var relative = Path.GetRelativePath(sourceRoot, path).Replace('\\', '/');
            if (!Path.GetDirectoryName(path)!.Equals(sourceRoot, StringComparison.Ordinal))
            {
                yield return $"'{relative}' is nested; local templates must be top-level.";
                continue;
            }

            if (file.StartsWith("resource-", StringComparison.Ordinal))
            {
                yield return $"'{file}' is ambiguous legacy ownership for '{owner}' / '{resource}'. "
                    + $"Preserve the existing canonical candidate and supply '{canonical}' after manual ownership resolution.";
                continue;
            }

            yield return $"'{file}' is a legacy resource source for '{owner}' / '{resource}'. "
                + $"Manually rename it to '{canonical}' after checking ownership; do not infer its kind from contents. "
                + "Run 'dydo template update' for shipped sources.";
        }
    }

    private static bool HasValidResourceReference(string ownerPath, string projectRoot, string resource)
    {
        try
        {
            var content = File.ReadAllText(ownerPath);
            var resolved = ValidateTemplate(ownerPath, content, projectRoot);
            Parse(Path.GetFileName(ownerPath), content);
            return ResourceLinkRegex().Matches(resolved)
                .Any(match => match.Groups[1].Value.Equals(resource, StringComparison.Ordinal));
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static bool HasRecordedLegacyResource(string file, DydoConfig config) =>
        TryParseLegacyResource(file, out var owner, out var resource)
        && config.Skills.TryGetValue(owner, out var switchEntry)
        && switchEntry.Resources?.Contains(resource, StringComparer.Ordinal) == true;

    private static bool TryParseLegacyResource(string file, out string owner, out string resource)
    {
        owner = "";
        resource = "";
        var stem = file[..^".template.md".Length];
        var delimiter = stem.IndexOf("-resource-", StringComparison.Ordinal);
        if (delimiter < 0)
            return false;
        owner = stem[..delimiter];
        resource = stem[(delimiter + "-resource-".Length)..];
        return ConfigService.IsValidSlug(owner) && ConfigService.IsValidSlug(resource);
    }

    private static SkillSwitchConfig Clone(SkillSwitchConfig value) => new()
    {
        Enabled = value.Enabled,
        Origin = value.Origin,
        EmitAgent = value.EmitAgent,
        CodexMetadata = value.CodexMetadata,
        Resources = value.Resources?.ToList()
    };

    private static string ValidateTemplate(string path, string content, string projectRoot)
    {
        var fields = FrontmatterParser.ParseFields(content)
            ?? throw new InvalidDataException($"Skill template '{Path.GetFileName(path)}' has no frontmatter.");
        var unknown = fields.Keys.Where(key => !AllowedFields.Contains(key)).ToList();
        if (unknown.Count > 0)
            throw new InvalidDataException($"Skill template '{Path.GetFileName(path)}' has unknown frontmatter key(s): {string.Join(", ", unknown)}.");
        if (!fields.TryGetValue("description", out var description) || string.IsNullOrWhiteSpace(Unquote(description)))
            throw new InvalidDataException($"Skill template '{Path.GetFileName(path)}' has a missing or blank description.");
        if (string.IsNullOrWhiteSpace(FrontmatterParser.StripFrontmatter(content)))
            throw new InvalidDataException($"Skill template '{Path.GetFileName(path)}' has a blank body.");
        if (fields.TryGetValue("emit", out var emit) && emit is not ("agent" or "skill"))
            throw new InvalidDataException($"Skill template '{Path.GetFileName(path)}' emit must be 'agent' or 'skill'.");
        if (fields.TryGetValue("invocation", out var invocation) && invocation is not ("automatic" or "explicit"))
            throw new InvalidDataException($"Skill template '{Path.GetFileName(path)}' invocation must be 'automatic' or 'explicit'.");
        foreach (var key in new[] { "read-only", "delegates", "web" })
        {
            if (fields.TryGetValue(key, out var value) && value is not ("true" or "false"))
                throw new InvalidDataException($"Skill template '{Path.GetFileName(path)}' {key} must be a strict boolean.");
        }
        var emitsAgent = !fields.TryGetValue("emit", out emit) || emit == "agent";
        if (!emitsAgent && new[] { "read-only", "delegates", "web" }.Any(fields.ContainsKey))
            throw new InvalidDataException($"Skill-only template '{Path.GetFileName(path)}' declares agent-only metadata.");
        if (emitsAgent && fields.GetValueOrDefault("invocation") == "explicit")
            throw new InvalidDataException($"Agent template '{Path.GetFileName(path)}' cannot use explicit invocation.");

        var resolved = ResolveIncludesStrict(content, projectRoot, path);
        ValidateMustReads(resolved, projectRoot, path);
        return resolved;
    }

    private static string ResolveIncludesStrict(string content, string projectRoot, string sourcePath)
        => TemplateGenerator.ResolveIncludes(content, projectRoot);

    private static void ValidateMustReads(string content, string projectRoot, string sourcePath)
    {
        var section = MustReadsRegex().Match(content);
        if (!section.Success)
            return;
        foreach (Match link in LinkRegex().Matches(section.Value))
        {
            var authoredTarget = link.Groups[1].Value;
            if (authoredTarget.StartsWith('#')
                || authoredTarget.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || authoredTarget.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                continue;
            var fragment = authoredTarget.IndexOf('#');
            var target = (fragment < 0 ? authoredTarget : authoredTarget[..fragment])
                .Replace('/', Path.DirectorySeparatorChar);
            var climbCount = Regex.Matches(target, @"^\.\.[\\/]", RegexOptions.CultureInvariant).Count;
            if (target.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                climbCount = target.Split(Path.DirectorySeparatorChar).TakeWhile(part => part == "..").Count();
            if (climbCount > 3 || Path.IsPathRooted(target))
                throw new InvalidDataException($"Skill template '{Path.GetFileName(sourcePath)}' has a Must-Read outside the project: '{link.Groups[1].Value}'.");
            var normalized = Regex.Replace(target, @"^(\.\.[\\/])+", "");
            var fullPath = Path.GetFullPath(Path.Combine(projectRoot,
                normalized.StartsWith("dydo" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                    ? normalized
                    : Path.Combine("dydo", normalized)));
            var root = Path.GetFullPath(projectRoot) + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(fullPath))
                throw new InvalidDataException($"Skill template '{Path.GetFileName(sourcePath)}' has a Must-Read outside the project or missing: '{link.Groups[1].Value}'.");
        }
    }

    [GeneratedRegex(@"^## Must-Reads\b.*?(?=^## |\z)", RegexOptions.Singleline | RegexOptions.Multiline)]
    private static partial Regex MustReadsRegex();

    [GeneratedRegex(@"\]\(([^)\s]+)\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"\]\(resources/([a-z0-9]+(?:-[a-z0-9]+)*)\.md\)")]
    private static partial Regex ResourceLinkRegex();

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
            Web = fields.TryGetValue("web", out var w)
                && w.Equals("true", StringComparison.OrdinalIgnoreCase),
            ArgumentHint = fields.TryGetValue("argument-hint", out var h) ? Unquote(h) : null,
        };
    }

    /// <summary>
    /// Strips one surrounding pair of double or single quotes. The frontmatter reader returns the
    /// raw value, and a hint is authored quoted, so the quotes would otherwise compile into the
    /// hint the host shows.
    /// </summary>
    private static string Unquote(string value) =>
        value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0]
            ? value[1..^1]
            : value;

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
