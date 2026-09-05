namespace DynaDocs.Services;

using System.Reflection;
using System.Text.RegularExpressions;
using DynaDocs.Commands;
using DynaDocs.Models;

/// <summary>
/// Generates documentation files by reading templates from embedded resources
/// and replacing placeholders like {{AGENT_NAME}}, {{PROJECT_NAME}}.
/// </summary>
public static class TemplateGenerator
{
    private static readonly Assembly _assembly = Assembly.GetExecutingAssembly();

    /// <summary>
    /// Lists a skill's resource templates — files named
    /// `&lt;skill&gt;-resource-&lt;name&gt;.template.md` ("resource" is the protected word) — as
    /// (fileName, content) pairs. `dydo sync` compiles each into the skill folder as
    /// `resources/&lt;name&gt;.md`.
    /// </summary>
    public static IEnumerable<(string FileName, string Content)> GetSkillResources(string skillName)
    {
        foreach (var templateName in GetSkillResourceTemplateNames(skillName))
        {
            var name = templateName[$"{skillName}-resource-".Length..^".template.md".Length];
            yield return ($"{name}.md", ReadBuiltInTemplate(templateName));
        }
    }

    /// <summary>
    /// Embedded template names matching `&lt;skill&gt;-resource-*.template.md`.
    /// </summary>
    public static IReadOnlyList<string> GetSkillResourceTemplateNames(string skillName)
    {
        var prefix = $"DynaDocs.Templates.{skillName}-resource-";
        return _assembly.GetManifestResourceNames()
            .Where(r => r.StartsWith(prefix) && r.EndsWith(".template.md"))
            .Select(r => r["DynaDocs.Templates.".Length..])
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Reads a template from embedded resources.
    /// </summary>
    private static string? ReadEmbeddedTemplate(string templateName)
    {
        var resourceName = $"DynaDocs.Templates.{templateName}";
        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Reads a shipped template from the executable's embedded snapshot.
    /// </summary>
    public static string ReadBuiltInTemplate(string templateName)
    {
        var content = ReadEmbeddedTemplate(templateName);
        if (content != null)
            return content;

        throw new FileNotFoundException($"Built-in template not found: {templateName}");
    }

    /// <summary>
    /// The shipped skill templates (skill-*.template.md) — the sources `dydo sync` compiles.
    /// Enumerated from embedded resources only, so a consumer's working directory cannot
    /// change the executable's shipped inventory.
    ///
    /// A retired skill's template is excluded even while the file still ships through a
    /// transition. This is the single place the exclusion has to happen: everything downstream
    /// reads the shipped set, so a retired name is not discovered.
    /// </summary>
    public static IReadOnlyList<string> GetBuiltInSkillTemplateNames()
    {
        const string prefix = "DynaDocs.Templates.skill-";
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var resource in _assembly.GetManifestResourceNames()
                     .Where(r => r.StartsWith(prefix) && r.EndsWith(".template.md")))
            names.Add(resource["DynaDocs.Templates.".Length..]);

        names.ExceptWith(SyncCommand.RetiredSkills.Select(name => $"skill-{name}.template.md"));
        return names.ToList();
    }

    /// <summary>
    /// The shipped template inventory: every skill template (skill-*.template.md) — the source
    /// `dydo sync` compiles into native agents and skills — plus each skill's resource templates
    /// (&lt;skill&gt;-resource-&lt;name&gt;.template.md).
    /// </summary>
    public static IReadOnlyList<string> GetAllTemplateNames()
    {
        var names = new List<string>();
        foreach (var templateFile in GetBuiltInSkillTemplateNames())
        {
            names.Add(templateFile);
            var skillName = templateFile["skill-".Length..^".template.md".Length];
            names.AddRange(GetSkillResourceTemplateNames(skillName));
        }
        return names;
    }

    private static string? GetTemplateAdditionsPath(string? basePath = null)
    {
        basePath ??= Environment.CurrentDirectory;

        var inside = Path.Combine(basePath, "_system", "template-additions");
        if (Directory.Exists(inside))
            return inside;

        var fromRoot = Path.Combine(basePath, "dydo", "_system", "template-additions");
        if (Directory.Exists(fromRoot))
            return fromRoot;

        return null;
    }

    public static string ResolveIncludes(string content, string? basePath = null)
    {
        var additionsPath = GetTemplateAdditionsPath(basePath);

        content = Regex.Replace(content, @"\{\{include:([a-zA-Z0-9_-]+)\}\}", match =>
        {
            var name = match.Groups[1].Value;
            if (additionsPath == null) return "";

            var filePath = Path.Combine(additionsPath, $"{name}.md");
            return File.Exists(filePath) ? File.ReadAllText(filePath).TrimEnd() : "";
        });

        // Collapse the blank-line pile-up an empty include leaves behind. Must match
        // CRLF runs too: template sources are CRLF on Windows checkouts, and an
        // uncollapsed \r\n\r\n\r\n survives the .claude/ LF-normalization as \n\n\n.
        return Regex.Replace(content, @"(\r?\n){3,}", "\n\n");
    }

    /// <summary>
    /// The runtime entry-point file at the project root — materialized as CLAUDE.md
    /// (Claude Code) and AGENTS.md (Codex) from one runtime-neutral template.
    /// Authored in Templates/entry-point.template.md ({{PROJECT_NAME}} placeholder).
    /// </summary>
    public static string GenerateEntryPointMd(string projectName)
    {
        return ReadBuiltInTemplate("entry-point.template.md")
            .Replace("{{PROJECT_NAME}}", projectName)
            .TrimEnd('\r', '\n');
    }

    /// <summary>
    /// dydo/index.md - The main entry point explaining the system.
    /// </summary>
    public static string GenerateIndexMd() => ReadBuiltInTemplate("index.template.md");

    /// <summary>
    /// dydo/guides/working-tree-contract.md — the shared branch, worktree and cleanup contract
    /// every parallel agent follows (DR 045 §8). A framework document, so `dydo init` scaffolds
    /// it and `dydo template update` tracks it.
    /// </summary>
    public static string GenerateWorkingTreeContractMd() =>
        ReadBuiltInTemplate("working-tree-contract.template.md");

    /// <summary>
    /// dydo/reference/linear-workspace-standard.md — the canonical Linear labels, statuses and
    /// templates expected by the agent system. The template is intentionally empty until its
    /// dedicated HITL pass.
    /// </summary>
    public static string GenerateLinearWorkspaceStandardMd() =>
        ReadBuiltInTemplate("linear-workspace-standard.template.md");

    /// <summary>
    /// Architecture overview template.
    /// </summary>
    public static string GenerateArchitectureMd() => ReadBuiltInTemplate("architecture.template.md");

    /// <summary>
    /// Welcome page for humans.
    /// </summary>
    public static string GenerateWelcomeMd() => ReadBuiltInTemplate("welcome.template.md");

    /// <summary>
    /// Coding standards template.
    /// </summary>
    public static string GenerateCodingStandardsMd() => ReadBuiltInTemplate("coding-standards.template.md");

    /// <summary>
    /// Generate a hub _index.md file for a folder.
    /// </summary>
    public static string GenerateHubIndex(string folderName, string description, string area)
    {
        var title = char.ToUpper(folderName[0]) + folderName[1..];

        return $"""
            ---
            area: {area}
            type: hub
            ---

            # {title}

            {description}

            ---

            ## Contents

            *Add links to documents in this section.*
            """;
    }

    /// <summary>
    /// Generate the about.md file for understanding the project.
    /// </summary>
    public static string GenerateAboutMd() => ReadBuiltInTemplate("about.template.md");

    /// <summary>
    /// Generate the files-off-limits.md template.
    /// This file defines paths that are globally blocked for all agents.
    /// </summary>
    public static string GenerateFilesOffLimitsMd() => ReadBuiltInTemplate("files-off-limits.template.md");

    /// <summary>
    /// Generate the dydo commands reference document.
    /// </summary>
    public static string GenerateDydoCommandsMd() => ReadBuiltInTemplate("dydo-commands.template.md");

    /// <summary>
    /// Generate the dydo glossary reference document.
    /// </summary>
    public static string GenerateDydoGlossaryMd() => ReadBuiltInTemplate("dydo-glossary.template.md");

    /// <summary>
    /// Generate the writing docs reference document.
    /// </summary>
    public static string GenerateWritingDocsMd() => ReadBuiltInTemplate("writing-docs.template.md");

    /// <summary>
    /// Generate the glossary.md reference document.
    /// </summary>
    public static string GenerateGlossaryMd() => ReadBuiltInTemplate("glossary.template.md");

    /// <summary>
    /// Generate the about-dynadocs.md reference document.
    /// </summary>
    public static string GenerateAboutDynadocsMd() => ReadBuiltInTemplate("about-dynadocs.template.md");

    /// <summary>
    /// Get all asset file names that should be copied to _assets/. Currently empty: the
    /// pre-DR-041 architecture diagram was retired (issue 0301) — it depicted the removed
    /// claim/inbox/agent-workspace runtime. The scaffolded _assets/ folder remains for
    /// project-owned assets, and the copy/hash/update plumbing stays for future assets.
    /// </summary>
    public static IReadOnlyList<string> GetAssetNames()
    {
        return Array.Empty<string>();
    }

    /// <summary>
    /// Read a binary asset from embedded resources.
    /// </summary>
    public static byte[]? ReadEmbeddedAsset(string assetName)
    {
        var resourceName = $"DynaDocs.Templates.Assets.{assetName}";
        using var stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            return null;

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Generate a hub _index.md file for a project subfolder.
    /// Minimal content since the meta file has the details.
    /// </summary>
    public static string GenerateProjectSubfolderHub(string folderName, string description)
    {
        var title = char.ToUpper(folderName[0]) + folderName[1..];

        return $"""
            ---
            area: project
            type: hub
            ---

            # {title}

            {description}

            ## Contents

            *No documents in this folder yet.*
            """;
    }

    /// <summary>
    /// Generate the _decisions.md meta file describing the decisions folder.
    /// </summary>
    public static string GenerateDecisionsMetaMd()
    {
        return ReadTemplateOrThrow("_decisions.template.md");
    }

    /// <summary>
    /// Generate the _changelog.md meta file describing the changelog folder.
    /// </summary>
    public static string GenerateChangelogMetaMd()
    {
        return ReadTemplateOrThrow("_changelog.template.md");
    }

    /// <summary>
    /// Generate the _pitfalls.md meta file describing the pitfalls folder.
    /// </summary>
    public static string GeneratePitfallsMetaMd()
    {
        return ReadTemplateOrThrow("_pitfalls.template.md");
    }

    /// <summary>
    /// Generate the _future-features.md meta file describing the future-features folder.
    /// </summary>
    public static string GenerateFutureFeaturesMetaMd()
    {
        return ReadTemplateOrThrow("_future-features.template.md");
    }

    /// <summary>
    /// Generate the _understand.md meta file describing the understand folder.
    /// </summary>
    public static string GenerateUnderstandMetaMd()
    {
        return ReadTemplateOrThrow("_understand.template.md");
    }

    /// <summary>
    /// Generate the _guides.md meta file describing the guides folder.
    /// </summary>
    public static string GenerateGuidesMetaMd()
    {
        return ReadTemplateOrThrow("_guides.template.md");
    }

    /// <summary>
    /// Generate the _reference.md meta file describing the reference folder.
    /// </summary>
    public static string GenerateReferenceMetaMd()
    {
        return ReadTemplateOrThrow("_reference.template.md");
    }

    /// <summary>
    /// Generate the _project.md meta file describing the project folder.
    /// </summary>
    public static string GenerateProjectMetaMd()
    {
        return ReadTemplateOrThrow("_project.template.md");
    }

    /// <summary>
    /// Read a template, throwing if not found (for required templates).
    /// </summary>
    private static string ReadTemplateOrThrow(string templateName)
    {
        var content = ReadEmbeddedTemplate(templateName);
        if (content != null)
            return content;

        throw new FileNotFoundException($"Required template not found: {templateName}");
    }

}
