namespace DynaDocs.Services;

using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
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
    /// Checks project-local templates first, then falls back to embedded resources.
    /// </summary>
    private static string ReadTemplate(string templateName, string? basePath = null)
    {
        // Check project-local templates first
        var projectPath = GetProjectTemplatesPath(basePath);
        if (projectPath != null)
        {
            var localFile = Path.Combine(projectPath, templateName);
            if (File.Exists(localFile))
                return File.ReadAllText(localFile);
        }

        // Fall back to embedded resources
        var content = ReadEmbeddedTemplate(templateName);
        if (content != null)
            return content;

        throw new FileNotFoundException($"Template not found: {templateName}");
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
    /// Get all template file names that can be copied to _system/templates/.
    /// </summary>
    public static IReadOnlyList<string> GetAllTemplateNames()
    {
        // The role mode templates (mode-*.template.md) — the source `dydo sync` compiles into
        // native agents. The per-agent-workspace agent-workflow.template.md was removed with the
        // 26-agent roster (DR-041).
        var names = new List<string>();
        foreach (var role in RoleDefinitionService.GetBaseRoleDefinitions())
        {
            if (!string.IsNullOrEmpty(role.TemplateFile))
                names.Add(role.TemplateFile);
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
            - Read [../guides/how-to-use-docs.md](../guides/how-to-use-docs.md) for workflow commands
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

            ---

            ## Next Steps

            After understanding coding standards:
            - Read [how-to-use-docs.md](how-to-use-docs.md) for workflow commands
            """;
    }

    /// <summary>
    /// How to use docs - guide to navigating the documentation.
    /// Reads from how-to-use-docs.template.md if available.
    /// </summary>
    public static string GenerateHowToUseDocsMd()
    {
        try
        {
            return ReadTemplate("how-to-use-docs.template.md");
        }
        catch (FileNotFoundException)
        {
            return GenerateFallbackHowToUseDocsMd();
        }
    }

    internal static string GenerateFallbackHowToUseDocsMd()
    {
        return """
            ---
            area: guides
            type: guide
            ---

            # How to Use These Docs

            This documentation is designed for AI agents. It follows JITI (Just-In-Time Information) - you don't need to read everything upfront. Navigate to what you need, when you need it.

            ---

            ## Documentation Structure

            | Folder | Contains | When to Read |
            |--------|----------|--------------|
            | `understand/` | Project overview, architecture, domain context | Starting a new task |
            | `guides/` | How-to guides, coding standards | When doing specific work |
            | `reference/` | Command reference, API specs, config | When you need exact details |
            | `project/` | Decisions, changelog, tasks, pitfalls | When you need history/context |

            ---

            ## Document Types

            The frontmatter at the top of each doc tells you what kind it is:

            | Type | Purpose |
            |------|---------|
            | `context` | Background information, overviews |
            | `guide` | Step-by-step instructions |
            | `reference` | Look-up information |
            | `hub` | Index pages linking to other docs |

            ---

            ## Navigation

            ### Index Files

            Every folder has an `_index.md` — the table of contents for that area:

            | Index | Contents |
            |-------|----------|
            | `understand/_index.md` | Context and architecture docs |
            | `guides/_index.md` | How-to guides |
            | `reference/_index.md` | Reference docs |
            | `project/_index.md` | Decisions, changelog, pitfalls |

            ### Related Links

            Docs link to each other via **Related** sections at the bottom. Follow links only when you need more detail.

            ---

            ## Exploring Connections

            Use `dydo graph` to see how documents connect:

            ```bash
            dydo graph dydo/understand/architecture.md
            ```

            This shows what links to and from a document - useful for finding related context.

            ---

            ## Key Reference Documents

            | Document | Purpose |
            |----------|---------|
            | `glossary.md` | Domain-specific terms |
            | `project/decisions/` | Why architectural choices were made |
            | `project/changelog/` | What changed and when |
            | `project/pitfalls/` | Known gotchas to avoid |

            ---

            ## Related

            - [Architecture](../understand/architecture.md) — Project structure
            - [Coding Standards](./coding-standards.md) — Code conventions
            - [dydo Commands Reference](../reference/dydo-commands.md) — Full command documentation
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

            Complete reference for all `dydo` commands.

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

            ## Project Commands

            | Command | Description |
            |---------|-------------|
            | `dydo task create\|list\|ready-for-review\|done <name>` | Manage tasks |
            | `dydo issue create\|list\|resolve` | Manage issues |
            | `dydo review complete <task>` | Complete a code review |

            ## Enforcement & Config

            | Command | Description |
            |---------|-------------|
            | `dydo guard` | Check permissions (used by hooks) |
            | `dydo validate` | Validate config, roles, and system integrity |
            | `dydo template update` | Update framework templates and docs |
            | `dydo roles list\|reset\|create` | Manage role definitions |
            | `dydo model cap\|uncap <model>` | Time-boxed model outage swaps |

            ## Integrations & Utility

            | Command | Description |
            |---------|-------------|
            | `dydo notion connect\|sync\|reset\|reveal-token` | Notion projection |
            | `dydo watchdog` | Background monitoring daemon |
            | `dydo completions <shell>` | Generate shell completions |
            | `dydo version` | Show version |
            | `dydo help` | Show help |

            ---

            Run `dydo <command> --help` for detailed usage of each command.
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
        return """
            ---
            area: reference
            type: reference
            ---

            # DynaDocs (dydo)

            Documentation-driven context and agent orchestration for AI coding assistants.

            100% local, 100% under your control.

            ## The Problem

            AI code editors need persistence. Without it, each session starts fresh and the agent has to gather context about the project before it can even begin working on your actual task.

            ## The Solution

            DynaDocs combines an agent-friendly documentation format with a CLI tool for deterministic rule enforcement and framework management.

            ![DynaDocs Architecture](./../_assets/dydo-diagram.svg)

            ## Workflow Flags

            | Flag | Workflow |
            |------|----------|
            | `--inbox` | Process dispatched work |

            ## Agent Roles

            | Role | Can Edit | Purpose |
            |------|----------|---------|
            | `co-thinker` | `decisions/**`, agent workspace | Explore ideas, scope requirements |
            | `code-writer` | source + test directories | Implement features |
            | `test-writer` | test directories, `pitfalls/**`, agent workspace | Write tests, report bugs |
            | `reviewer` | agent workspace | Review code |
            | `docs-writer` | `dydo/**` (except agents/) | Write documentation |

            ## More Information

            - **Project Repository**: [github.com/bodnarbalazs/dydo](https://github.com/bodnarbalazs/dydo)
            - **Command Reference**: [dydo-commands.md](./dydo-commands.md)

            ## License

            MIT
            """;
    }

    /// <summary>
    /// Get all asset file names that should be copied to _assets/.
    /// </summary>
    public static IReadOnlyList<string> GetAssetNames()
    {
        return new[]
        {
            "dydo-diagram.svg"
        };
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
    /// Generate the _tasks.md meta file describing the tasks folder.
    /// </summary>
    public static string GenerateTasksMetaMd()
    {
        return ReadTemplateOrThrow("_tasks.template.md");
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
    /// Generate the _issues.md meta file describing the issues folder.
    /// </summary>
    public static string GenerateIssuesMetaMd()
    {
        return ReadTemplateOrThrow("_issues.template.md");
    }

    /// <summary>
    /// Generate the _backlog.md meta file describing the backlog folder.
    /// </summary>
    public static string GenerateBacklogMetaMd()
    {
        return ReadTemplateOrThrow("_backlog.template.md");
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
