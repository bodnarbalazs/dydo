namespace DynaDocs.Services;

using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using DynaDocs.Commands;
using DynaDocs.Models;

/// <summary>
/// Generates documentation files by reading templates from embedded resources
/// and replacing placeholders like {{AGENT_NAME}}, {{PROJECT_NAME}}.
/// Supports project-local template overrides in dydo/_system/templates/.
/// </summary>
public static class TemplateGenerator
{
    private static readonly Assembly _assembly = Assembly.GetExecutingAssembly();

    /// <summary>
    /// Gets the path to project-local templates if the folder exists.
    /// Handles both cases: basePath is the dydo folder, or basePath is the project root.
    /// </summary>
    private static string? GetProjectTemplatesPath(string? basePath = null)
    {
        basePath ??= Environment.CurrentDirectory;

        // If basePath is the dydo folder itself (used by FolderScaffolder)
        var templatesInside = Path.Combine(basePath, "_system", "templates");
        if (Directory.Exists(templatesInside))
            return templatesInside;

        // If basePath is the project root (default case)
        var templatesFromRoot = Path.Combine(basePath, "dydo", "_system", "templates");
        if (Directory.Exists(templatesFromRoot))
            return templatesFromRoot;

        return null;
    }

    /// <summary>
    /// Lists a role's skill resource templates — files named
    /// `&lt;role&gt;-resource-&lt;name&gt;.template.md` ("resource" is the protected word) — as
    /// (fileName, content) pairs. `dydo sync` compiles each into the skill folder as
    /// `resources/&lt;name&gt;.md`. Content resolves through <see cref="ReadTemplate"/>, so
    /// project-local overrides in `dydo/_system/templates/` apply like any other template.
    /// </summary>
    public static IEnumerable<(string FileName, string Content)> GetSkillResources(string roleName)
    {
        foreach (var templateName in GetSkillResourceTemplateNames(roleName))
        {
            var name = templateName[$"{roleName}-resource-".Length..^".template.md".Length];
            yield return ($"{name}.md", ReadTemplate(templateName));
        }
    }

    /// <summary>
    /// The workflow harness scripts dydo ships (Templates/workflow-&lt;name&gt;.js — "workflow-"
    /// is the protected prefix). `dydo sync` compiles each to `.claude/workflows/&lt;name&gt;.js`.
    /// Claude-only for now; a codex equivalent gets a matching emit path when one exists.
    /// </summary>
    public static IEnumerable<(string FileName, string Content)> GetWorkflowScripts()
    {
        const string prefix = "DynaDocs.Templates.workflow-";
        foreach (var resource in _assembly.GetManifestResourceNames()
                     .Where(r => r.StartsWith(prefix) && r.EndsWith(".js"))
                     .OrderBy(r => r, StringComparer.Ordinal))
        {
            using var stream = _assembly.GetManifestResourceStream(resource);
            if (stream == null) continue;
            using var reader = new StreamReader(stream);
            yield return (resource[prefix.Length..], reader.ReadToEnd());
        }
    }

    /// <summary>
    /// Embedded template names matching `&lt;role&gt;-resource-*.template.md`.
    /// </summary>
    public static IReadOnlyList<string> GetSkillResourceTemplateNames(string roleName)
    {
        var prefix = $"DynaDocs.Templates.{roleName}-resource-";
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
    /// Reads a template file and returns its content.
    /// Checks project-local templates first, then falls back to the built-in template
    /// (source Templates/ in dev-mode, embedded resources otherwise).
    /// </summary>
    internal static string ReadTemplate(string templateName, string? basePath = null)
    {
        // Check project-local templates first
        var projectPath = GetProjectTemplatesPath(basePath);
        if (projectPath != null)
        {
            var localFile = Path.Combine(projectPath, templateName);
            if (File.Exists(localFile))
                return File.ReadAllText(localFile);
        }

        return ReadBuiltInTemplate(templateName);
    }

    /// <summary>
    /// Read a built-in template (ignores project-local overrides).
    /// Used by FolderScaffolder to copy templates to _system/templates/.
    /// </summary>
    public static string ReadBuiltInTemplate(string templateName)
    {
        // Dev-mode: when running within the DynaDocs source tree,
        // prefer source Templates/ over potentially stale embedded resources
        var devPath = Path.Combine("Templates", templateName);
        if (File.Exists(devPath) && File.Exists("DynaDocs.csproj"))
            return File.ReadAllText(devPath);

        var content = ReadEmbeddedTemplate(templateName);
        if (content != null)
            return content;

        throw new FileNotFoundException($"Built-in template not found: {templateName}");
    }

    /// <summary>
    /// The shipped skill templates (skill-*.template.md) — the roles `dydo sync` compiles.
    /// Enumerated from embedded resources, plus source Templates/ in dev-mode so a
    /// not-yet-rebuilt template still counts.
    ///
    /// A retired role's template is excluded even while the file still ships through a
    /// transition. This is the single place the exclusion has to happen: everything downstream
    /// reads the shipped set, so a retired name is not discovered, not mirrored into
    /// dydo/_system/templates/ by `dydo init`, and not hash-tracked — which is what lets
    /// `dydo template update` prune an already-mirrored copy as stale instead of reviving the
    /// role forever. A project that deliberately authors its own skill-&lt;name&gt;.template.md
    /// still gets the role: that copy is untracked, survives the prune, and joins discovery.
    /// </summary>
    public static IReadOnlyList<string> GetBuiltInSkillTemplateNames()
    {
        const string prefix = "DynaDocs.Templates.skill-";
        var names = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var resource in _assembly.GetManifestResourceNames()
                     .Where(r => r.StartsWith(prefix) && r.EndsWith(".template.md")))
            names.Add(resource["DynaDocs.Templates.".Length..]);

        // Dev-mode parity with ReadBuiltInTemplate: prefer the source tree's Templates/.
        if (File.Exists("DynaDocs.csproj") && Directory.Exists("Templates"))
        {
            names.RemoveWhere(n => !File.Exists(Path.Combine("Templates", n)));
            foreach (var file in Directory.GetFiles("Templates", "skill-*.template.md"))
                names.Add(Path.GetFileName(file));
        }

        names.ExceptWith(SyncCommand.RetiredManagedRoles.Select(role => $"skill-{role}.template.md"));
        return names.ToList();
    }

    /// <summary>
    /// Project-local skill templates in dydo/_system/templates/ — overrides of shipped
    /// roles and, when the name is new, custom roles.
    /// </summary>
    public static IReadOnlyList<string> GetProjectSkillTemplateNames(string? basePath = null)
    {
        var projectPath = GetProjectTemplatesPath(basePath);
        if (projectPath == null)
            return [];

        return Directory.GetFiles(projectPath, "skill-*.template.md")
            .Select(Path.GetFileName)
            .Where(n => n != null)
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Legacy project-local role templates retained only to warn users during the filename
    /// transition. They are never included in role discovery or compilation.
    /// </summary>
    internal static IReadOnlyList<string> GetProjectLegacyModeTemplateNames(string? basePath = null)
    {
        var projectPath = GetProjectTemplatesPath(basePath);
        if (projectPath == null)
            return [];

        return Directory.GetFiles(projectPath, "mode-*.template.md")
            .Select(Path.GetFileName)
            .Where(n => n != null)
            .Select(n => n!)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Get all template file names that can be copied to _system/templates/.
    /// </summary>
    public static IReadOnlyList<string> GetAllTemplateNames()
    {
        // The role skill templates (skill-*.template.md) — the source `dydo sync` compiles into
        // native agents — plus each role's skill resource templates
        // (<role>-resource-<name>.template.md), which ride the same override/hash machinery.
        var names = new List<string>();
        foreach (var templateFile in GetBuiltInSkillTemplateNames())
        {
            names.Add(templateFile);
            var roleName = templateFile["skill-".Length..^".template.md".Length];
            names.AddRange(GetSkillResourceTemplateNames(roleName));
        }
        return names;
    }

    /// <summary>
    /// Replace placeholders in template content.
    /// </summary>
    private static string ReplacePlaceholders(string content, Dictionary<string, string> placeholders)
    {
        foreach (var (key, value) in placeholders)
        {
            content = content.Replace($"{{{{{key}}}}}", value);
        }
        return content;
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
        return ReadTemplate("entry-point.template.md")
            .Replace("{{PROJECT_NAME}}", projectName)
            .TrimEnd('\r', '\n');
    }

    /// <summary>
    /// dydo/index.md - The main entry point explaining the system.
    /// </summary>
    public static string GenerateIndexMd() => ReadTemplate("index.template.md");

    /// <summary>
    /// dydo/guides/working-tree-contract.md — the shared branch, worktree and cleanup contract
    /// every parallel agent follows (DR 045 §8). A framework document, so `dydo init` scaffolds
    /// it and `dydo template update` tracks it.
    /// </summary>
    public static string GenerateWorkingTreeContractMd() =>
        ReadTemplate("working-tree-contract.template.md");

    /// <summary>
    /// dydo/reference/linear-workspace-standard.md — the canonical Linear labels, statuses and
    /// templates expected by the agent system. The template is intentionally empty until its
    /// dedicated HITL pass.
    /// </summary>
    public static string GenerateLinearWorkspaceStandardMd() =>
        ReadTemplate("linear-workspace-standard.template.md");

    /// <summary>
    /// Architecture overview template.
    /// Reads from architecture.template.md if available.
    /// </summary>
    public static string GenerateArchitectureMd()
    {
        try
        {
            return ReadTemplate("architecture.template.md");
        }
        catch (FileNotFoundException)
        {
            return GenerateFallbackArchitectureMd();
        }
    }

    internal static string GenerateFallbackArchitectureMd()
    {
        return """
            ---
            area: understand
            type: concept
            ---

            # Architecture Overview

            > **Fill this in.** This document helps AI agents understand your codebase structure.

            ---

            ## Project Structure

            ```
            project/
            ├── src/                  # Source code
            ├── tests/                # Test files
            ├── dydo/                 # Documentation
            └── ...
            ```

            ---

            ## Key Components

            ### Component A

            *What this component does and its responsibilities.*

            ---

            ## Where to Find Things

            | Looking for... | Location |
            |----------------|----------|
            | *[Type of code]* | `path/` |

            ---

            ## Next Steps

            After understanding the architecture:
            - Read [../guides/coding-standards.md](../guides/coding-standards.md) for code style
            """;
    }

    /// <summary>
    /// Welcome page for humans.
    /// Reads from welcome.template.md if available.
    /// </summary>
    public static string GenerateWelcomeMd()
    {
        try
        {
            return ReadTemplate("welcome.template.md");
        }
        catch (FileNotFoundException)
        {
            return GenerateFallbackWelcomeMd();
        }
    }

    internal static string GenerateFallbackWelcomeMd()
    {
        return """
            ---
            area: general
            type: hub
            ---

            # Welcome

            Human-friendly entry point to the project documentation.

            ---

            ## Getting Started

            1. **[About](./understand/about.md)** — What this project is
            2. **[Architecture](./understand/architecture.md)** — How the system is structured
            3. **[Coding Standards](./guides/coding-standards.md)** — Read before writing code

            ---

            ## For AI Agents

            See [index.md](./index.md) for the AI agent entry point.
            """;
    }

    /// <summary>
    /// Coding standards template.
    /// Reads from coding-standards.template.md if available.
    /// </summary>
    public static string GenerateCodingStandardsMd()
    {
        try
        {
            return ReadTemplate("coding-standards.template.md");
        }
        catch (FileNotFoundException)
        {
            return GenerateFallbackCodingStandardsMd();
        }
    }

    internal static string GenerateFallbackCodingStandardsMd()
    {
        return """
            ---
            area: guides
            type: guide
            ---

            # Coding Standards

            > **TODO:** This is a template. Customize with your project's coding standards.

            ---

            ## General Principles

            1. **Readability over cleverness** — Code is read more than written
            2. **Consistency** — Follow existing patterns in the codebase
            3. **Simplicity** — Don't over-engineer; solve the problem at hand

            ---

            ## Naming Conventions

            | Type | Convention | Example |
            |------|------------|---------|
            | Files | kebab-case | `user-service.ts` |
            | Classes | PascalCase | `UserService` |
            | Functions | camelCase | `getUserById` |
            | Constants | UPPER_SNAKE | `MAX_RETRY_COUNT` |
            | Variables | camelCase | `currentUser` |

            """;
    }

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
    /// Reads from about.template.md if available.
    /// </summary>
    public static string GenerateAboutMd()
    {
        try
        {
            return ReadTemplate("about.template.md");
        }
        catch (FileNotFoundException)
        {
            return GenerateFallbackAboutMd();
        }
    }

    internal static string GenerateFallbackAboutMd()
    {
        return """
            ---
            area: understand
            type: context
            ---

            # About This Project

            > **Fill this in.** This is the first thing AI agents read. Make it count.

            *[Describe the project in 2-3 sentences]*

            ---

            *See [architecture.md](./architecture.md) for technical structure.*
            """;
    }

    /// <summary>
    /// Generate the files-off-limits.md template.
    /// This file defines paths that are globally blocked for all agents.
    /// </summary>
    public static string GenerateFilesOffLimitsMd()
    {
        try
        {
            return ReadTemplate("files-off-limits.template.md");
        }
        catch (FileNotFoundException)
        {
            return GenerateFallbackFilesOffLimitsMd();
        }
    }

    internal static string GenerateFallbackFilesOffLimitsMd()
    {
        return """
            ---
            type: config
            ---

            # Files Off-Limits

            Paths listed here are **blocked for ALL agents** regardless of role.
            These restrictions apply to all operations: read, write, and delete.

            ## Default Patterns

            ```
            # Environment files
            .env
            .env.*
            secrets.json
            **/secrets.json

            # Credentials and keys
            **/credentials.*
            **/*.pem
            **/*.key
            **/*.pfx
            **/id_rsa
            **/id_ed25519

            # Cloud configs
            **/.aws/**
            **/.azure/**

            # Package manager tokens
            **/.npmrc
            **/.pypirc
            ```

            ---

            Add project-specific sensitive files below.
            """;
    }

    /// <summary>
    /// Generate the dydo commands reference document.
    /// Reads from dydo-commands.template.md if available.
    /// </summary>
    public static string GenerateDydoCommandsMd()
    {
        try
        {
            return ReadTemplate("dydo-commands.template.md");
        }
        catch (FileNotFoundException)
        {
            return GenerateFallbackDydoCommandsMd();
        }
    }

    internal static string GenerateFallbackDydoCommandsMd()
    {
        return """
            ---
            area: reference
            type: reference
            ---

            # CLI Commands Reference

            Reference for dydo's local documentation, compilation, guard, and configuration commands.
            Live work is managed in Linear; dydo does not provide a second work-record command surface.

            Run `dydo help` for a quick overview of available commands.

            ---

            ## Setup Commands

            | Command | Description |
            |---------|-------------|
            | `dydo init <integration>` | Initialize project (claude, codex, none) |
            | `dydo init <int> --join` | Wire this machine's integration for an existing project |
            | `dydo sync` | Compile roles + docs into native Claude/Codex agents and skills |

            ## Documentation Commands

            | Command | Description |
            |---------|-------------|
            | `dydo check [path]` | Validate docs |
            | `dydo fix [path]` | Auto-fix issues |
            | `dydo index [path]` | Regenerate index |
            | `dydo graph <file>` | Show link graph |

            ## Work Boundary

            Linear owns live Initiatives, Projects, Issues, status, assignment, dependencies, and review
            state. dydo keeps durable Decisions, reviewed Project plans, audits, and assimilation evidence
            in Git; no dydo command reads, writes, caches, polls, or mirrors Linear.

            ## Enforcement & Config

            | Command | Description |
            |---------|-------------|
            | `dydo guard` | Check permissions (used by hooks) |
            | `dydo validate` | Validate config, roles, and system integrity |
            | `dydo template update` | Update framework templates and docs |
            | `dydo roles list\|reset\|create` | Manage role definitions |
            | `dydo model cap\|uncap <model>` | Time-boxed model outage swaps |

            ## Utility

            | Command | Description |
            |---------|-------------|
            | `dydo completions <shell>` | Generate shell completions |
            | `dydo version` | Show version |
            | `dydo help` | Show help |

            ---

            Run `dydo <command> --help` for detailed usage of each command.
            """;
    }

    /// <summary>
    /// Generate the dydo glossary reference document.
    /// Reads from dydo-glossary.template.md if available.
    /// </summary>
    public static string GenerateDydoGlossaryMd()
    {
        try
        {
            return ReadTemplate("dydo-glossary.template.md");
        }
        catch (FileNotFoundException)
        {
            return GenerateFallbackDydoGlossaryMd();
        }
    }

    internal static string GenerateFallbackDydoGlossaryMd()
    {
        return """
            ---
            area: reference
            type: reference
            ---

            # dydo Glossary

            The dydo system's terms, locked. One meaning per word, used consistently across docs,
            templates, skills, and records.
            """;
    }

    /// <summary>
    /// Generate the writing docs reference document.
    /// Reads from writing-docs.template.md if available.
    /// </summary>
    public static string GenerateWritingDocsMd()
    {
        try
        {
            return ReadTemplate("writing-docs.template.md");
        }
        catch (FileNotFoundException)
        {
            return GenerateFallbackWritingDocsMd();
        }
    }

    internal static string GenerateFallbackWritingDocsMd()
    {
        return """
            ---
            area: reference
            type: reference
            ---

            # Writing Documentation

            Reference for documentation conventions, structure, and validation rules.

            ---

            ## Frontmatter

            Every document requires YAML frontmatter:

            ```yaml
            ---
            area: guides
            type: guide
            ---
            ```

            ### Required Fields

            | Field | Values |
            |-------|--------|
            | `area` | `understand`, `guides`, `reference`, `general`, `frontend`, `backend`, `microservices`, `platform` |
            | `type` | `context`, `concept`, `guide`, `reference`, `hub`, `decision`, `pitfall`, `changelog` |

            ---

            ## Naming Conventions

            - **Files:** `kebab-case.md`
            - **Folders:** `kebab-case/`
            - **Hub files:** `_index.md` in each folder

            ---

            ## Validation

            ```bash
            dydo check              # Find issues
            dydo fix                # Auto-fix what's possible
            ```
            """;
    }

    /// <summary>
    /// Generate the glossary.md reference document.
    /// Reads from glossary.template.md if available.
    /// </summary>
    public static string GenerateGlossaryMd()
    {
        try
        {
            return ReadTemplate("glossary.template.md");
        }
        catch (FileNotFoundException)
        {
            return GenerateFallbackGlossaryMd();
        }
    }

    internal static string GenerateFallbackGlossaryMd()
    {
        return """
            ---
            area: general
            type: reference
            ---

            # Glossary

            Definitions of domain-specific terms used throughout this project.

            ---

            ## Project Terms

            ### Example Term

            Brief definition. Include context about when/where this concept applies.

            ---

            <!--
            Add terms alphabetically. Format:

            ### Term Name

            Definition. Context.
            -->
            """;
    }

    /// <summary>
    /// Generate the about-dynadocs.md reference document.
    /// Reads from about-dynadocs.template.md if available.
    /// </summary>
    public static string GenerateAboutDynadocsMd()
    {
        try
        {
            return ReadTemplate("about-dynadocs.template.md");
        }
        catch (FileNotFoundException)
        {
            return GenerateFallbackAboutDynadocsMd();
        }
    }

    internal static string GenerateFallbackAboutDynadocsMd()
    {
        // Keep this aligned with Templates/about-dynadocs.template.md (the embedded template this
        // falls back for): dydo authors and knows, the platform runs and coordinates (DR-041).
        return """
            ---
            area: reference
            type: reference
            ---

            # DynaDocs (dydo)

            Own your project's knowledge, use Linear for live work, and let native coding agents execute.

            ## The Problem

            AI code editors need persistence. Without it, each session starts fresh and the agent has to gather context about the project before it can even begin working on your actual task.

            ## The Solution

            DynaDocs keeps durable project knowledge explicit and versioned, compiles shared role methods
            for Claude Code and Codex, and enforces project rules through hooks. Linear owns the live
            Initiative/Project/Issue graph; the coding platform owns sessions, worktrees, delegation, and
            scheduling.

            ## Agent Roles

            Roles are compiled from skill templates by `dydo sync` into native Claude Code and Codex agents and skills:

            | Role | Shape | Purpose |
            |------|-------|---------|
            | `co-thinker` | skill | Explore ideas, scope requirements |
            | `project-planner` | agent + skill | Start and review the Project map |
            | `issue-planner` | agent + skill | Remove hidden decisions from one Issue |
            | `chief-of-staff` | skill | Triage Linear and route work |
            | `code-writer` | agent + skill | Implement features |
            | `test-writer` | agent + skill | Write tests, report bugs |
            | `reviewer` | agent + skill (read-only) | Review code |
            | `docs-writer` | agent + skill | Write documentation |

            ## More Information

            - **Project Repository**: [github.com/bodnarbalazs/dydo](https://github.com/bodnarbalazs/dydo)
            - **Command Reference**: [dydo-commands.md](./dydo-commands.md)

            ## License

            MIT
            """;
    }

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
