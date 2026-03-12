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
        var names = new List<string> { "agent-workflow.template.md" };
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

        return Regex.Replace(content, @"\n{3,}", "\n\n");
    }

    /// <summary>
    /// CLAUDE.md - The entry point. Brief, points to the documentation system.
    /// Located at project root.
    /// </summary>
    public static string GenerateClaudeMd(string projectName)
    {
        // CLAUDE.md is simple enough to keep inline - it's project-specific
        return $"""
            # {projectName}

            This project uses **DynaDocs** for documentation and AI agent workflow management.
            Before starting any task, read [dydo/index.md](dydo/index.md) and follow the onboarding process.
            """;
    }

    /// <summary>
    /// dydo/index.md - The main entry point explaining the system.
    /// Directs agents to claim an identity and read their workflow file.
    /// </summary>
    public static string GenerateIndexMd(List<string> agentNames)
    {
        try
        {
            // Use the template directly - it's designed for JITI flow
            return ReadTemplate("index.template.md");
        }
        catch (FileNotFoundException)
        {
            // Fall back to generated content if template not found
            var workflowLinks = string.Join("\n", agentNames.Select(name =>
                $"- [{name}](agents/{name}/workflow.md)"));
            return GenerateJitiIndexMd(agentNames, workflowLinks);
        }
    }

    internal static string GenerateJitiIndexMd(List<string> agentNames, string workflowLinks)
    {
        return """
            ---
            area: general
            type: hub
            ---

            # DynaDocs

            This is a **DynaDocs** project — a system that combines living documentation with AI agent workflow management.

            ---

            ## For AI Agents

            If you are an AI agent (Claude, GPT, etc.) starting a work session:

            ### Step 1: Choose Your Identity

            Each agent session operates under a **named identity**. This identity:
            - Determines your workspace location
            - Controls what files you can edit (based on role)
            - Tracks your current task and status

            **Available agent identities:**

            """ + workflowLinks + """


            Pick one and read its workflow file. The workflow file contains:
            - Your identity and how to claim it
            - Must-read documents (in order)
            - Role permissions and task workflow

            ### Step 2: Follow the Workflow

            After reading your workflow file, you will:
            1. Claim your identity with `dydo agent claim <name>`
            2. Read the must-read documents
            3. Set your role with `dydo agent role <role>`
            4. Begin your task

            ---

            ## For Humans

            DynaDocs provides:

            - **Living Documentation** — Validated, cross-linked documentation in `understand/`, `guides/`, `reference/`, `project/`
            - **Agent Workflow** — Multi-agent orchestration with role-based permissions
            - **Task Management** — Tracked tasks with handoff support

            ### Key Commands

            ```bash
            # Documentation
            dydo check              # Validate documentation
            dydo fix                # Auto-fix issues

            # Agent workflow
            dydo init claude        # Initialize with Claude Code hooks
            dydo agent claim auto   # Claim first available agent
            dydo whoami             # Show current identity
            dydo agent list         # List agents (current human)
            dydo agent list --all   # List all agents across humans
            ```

            See [guides/how-to-use-docs.md](guides/how-to-use-docs.md) for the complete command reference.

            ---

            ## Documentation Structure

            ```
            dydo/
            ├── index.md              ← You are here
            ├── understand/           # Core concepts & architecture
            │   ├── about.md          # Project context
            │   └── architecture.md
            ├── guides/               # How-to guides
            ├── reference/            # API & configuration
            ├── project/              # Decisions, changelog, tasks
            │   ├── tasks/
            │   ├── decisions/
            │   └── changelog/
            └── agents/               # Agent workspaces (gitignored)
                └── Adele/
                    ├── workflow.md   # Agent's workflow
                    └── modes/        # Mode-specific guidance
            ```

            ---

            ## Next Steps

            - **AI Agents:** Pick a workflow file above
            - **Humans:** See [guides/how-to-use-docs.md](guides/how-to-use-docs.md)
            """;
    }

    /// <summary>
    /// Workflow file for a specific agent.
    /// Reads from agent-workflow.template.md and replaces {{AGENT_NAME}}.
    /// </summary>
    public static string GenerateWorkflowFile(string agentName, string? basePath = null,
        List<string>? sourcePaths = null, List<string>? testPaths = null)
    {
        sourcePaths ??= ["src/**"];
        testPaths ??= ["tests/**"];

        try
        {
            var template = ReadTemplate("agent-workflow.template.md", basePath);
            var placeholders = new Dictionary<string, string>
            {
                ["AGENT_NAME"] = agentName,
                ["SOURCE_PATHS"] = FormatPathsList(sourcePaths),
                ["TEST_PATHS"] = FormatPathsList(testPaths),
                ["SOURCE_AND_TEST_PATHS"] = FormatPathsList([..sourcePaths, ..testPaths]),
                ["ROLE_TABLE"] = GenerateRoleTable(basePath)
            };
            var result = ReplacePlaceholders(template, placeholders);
            return ResolveIncludes(result, basePath);
        }
        catch (FileNotFoundException)
        {
            // Fall back to generated content if template not found
            return GenerateFallbackWorkflowFile(agentName, sourcePaths, testPaths);
        }
    }

    /// <summary>
    /// Generates a markdown role table from role definition files.
    /// Falls back to base role definitions if no role files exist on disk.
    /// basePath is the dydo folder (not project root).
    /// </summary>
    public static string GenerateRoleTable(string? basePath = null)
    {
        var roles = LoadRolesForTemplate(basePath);
        if (roles.Count == 0)
            roles = RoleDefinitionService.GetBaseRoleDefinitions();

        roles.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

        var lines = new List<string>
        {
            "| Role | Purpose | Mode File |",
            "|------|---------|-----------|"
        };

        foreach (var role in roles)
        {
            var modeFile = $"modes/{role.Name}.md";
            lines.Add($"| {role.Name} | {role.Description} | [{modeFile}]({modeFile}) |");
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Loads role definitions from disk for template generation.
    /// basePath may be the dydo folder or the project root.
    /// </summary>
    private static List<RoleDefinition> LoadRolesForTemplate(string? basePath)
    {
        if (basePath == null)
            return [];

        // basePath might be the dydo folder itself (from FolderScaffolder)
        var rolesDir = Path.Combine(basePath, "_system", "roles");
        if (!Directory.Exists(rolesDir))
        {
            // Or it might be the project root
            rolesDir = Path.Combine(basePath, "dydo", "_system", "roles");
            if (!Directory.Exists(rolesDir))
                return [];
        }

        var files = Directory.GetFiles(rolesDir, "*.role.json");
        if (files.Length == 0)
            return [];

        var roles = new List<RoleDefinition>();
        foreach (var file in files)
        {
            var json = File.ReadAllText(file);
            var role = JsonSerializer.Deserialize(json,
                Serialization.DydoConfigJsonContext.Default.RoleDefinition);
            if (role != null)
                roles.Add(role);
        }

        return roles;
    }

    /// <summary>
    /// Agent states overview file.
    /// Reads from agent-states.template.md and replaces {{AGENT_ROWS}}.
    /// </summary>
    public static string GenerateAgentStatesMd(IReadOnlyList<string> agentNames)
    {
        var rows = string.Join("\n", agentNames.Select(name =>
            $"| {name} | free | - | - | - |"));

        try
        {
            var template = ReadTemplate("agent-states.template.md");
            var placeholders = new Dictionary<string, string>
            {
                ["AGENT_ROWS"] = rows
            };
            return ReplacePlaceholders(template, placeholders);
        }
        catch (FileNotFoundException)
        {
            return GenerateFallbackAgentStatesMd(rows);
        }
    }

    internal static string GenerateFallbackAgentStatesMd(string rows)
    {
        return $"""
            ---
            last-updated: {DateTime.UtcNow:o}
            ---

            # Agent States

            | Agent | Status | Role | Task | Since |
            |-------|--------|------|------|-------|
            {rows}

            ## Pending Inbox

            | Agent | Items | Oldest |
            |-------|-------|--------|
            | (none) | - | - |

            ---

            <!--
            This file is updated by dydo commands.
            Check agent status: dydo agent list [--all]
            -->
            """;
    }

    internal static string GenerateFallbackWorkflowFile(string agentName,
        List<string> sourcePaths, List<string> testPaths)
    {
        var lowerName = agentName.ToLowerInvariant();
        var formattedSource = FormatPathsList(sourcePaths);
        var formattedTests = FormatPathsList(testPaths);
        var formattedAll = FormatPathsList([..sourcePaths, ..testPaths]);

        return $"""
            ---
            agent: {agentName}
            type: workflow
            ---

            # Workflow — {agentName}

            You are **{agentName}**. This file is your starting point for every work session.

            ---

            ## Immediate Action

            Run this command now to claim your identity:

            ```bash
            dydo agent claim {agentName}
            ```

            This registers you as {agentName} for this terminal session. You must claim before editing files.

            > **Note:** The command is case-insensitive. `dydo agent claim {lowerName}` also works.

            ---

            ## Must-Read Documents

            Read these in order. Each builds on the previous:

            | # | Document | What You'll Learn |
            |---|----------|-------------------|
            | 1 | [../understand/architecture.md](../understand/architecture.md) | Project structure, key components, how things connect |
            | 2 | [../guides/coding-standards.md](../guides/coding-standards.md) | Code style, naming conventions, patterns to follow |
            | 3 | [../guides/how-to-use-docs.md](../guides/how-to-use-docs.md) | DynaDocs commands, hooks, task workflow |

            After reading these, you'll understand:
            - The codebase architecture
            - How to write code that fits the project style
            - How to use dydo commands and complete tasks

            ---

            ## Your Workspace

            Your personal workspace is at `dydo/agents/{agentName}/`:

            ```
            dydo/agents/{agentName}/
            ├── state.md         # Your current state (managed by dydo)
            ├── .session         # Session info (managed by dydo)
            ├── inbox/           # Messages from other agents
            └── scratch/         # Your scratch space (optional)
            ```

            You can create `plan.md` or `notes.md` in your workspace for planning.

            ---

            ## Setting Your Role

            After claiming, set your role based on what you're doing:

            ```bash
            dydo agent role <role> --task <task-name>
            ```

            **Available roles:**

            | Role | Can Edit | Cannot Edit |
            |------|----------|-------------|
            | `code-writer` | {formattedSource}, {formattedTests} | `dydo/**`, `project/**` |
            | `reviewer` | `dydo/agents/{agentName}/**` | Everything else |
            | `co-thinker` | `dydo/agents/{agentName}/**`, `dydo/project/decisions/**` | {formattedAll} |
            | `docs-writer` | `dydo/**` | `dydo/agents/**`, {formattedSource} |
            | `planner` | `dydo/agents/{agentName}/**`, `dydo/project/tasks/**` | {formattedSource} |
            | `test-writer` | `dydo/agents/{agentName}/**`, {formattedTests}, `dydo/project/pitfalls/**` | {formattedSource} |

            The guard system enforces these permissions. If blocked, either:
            - Change to an appropriate role
            - Dispatch to another agent with the right role

            ---

            ## Quick Reference

            ```bash
            # Identity
            dydo agent claim {agentName}    # Claim this identity
            dydo whoami                     # Verify current identity
            dydo agent release              # Release when done

            # Role & Task
            dydo agent role <role>          # Set role
            dydo agent status               # Check current status

            # Inbox
            dydo inbox show                 # View your inbox
            dydo inbox clear --all          # Clear processed items

            # Dispatch
            dydo dispatch --role <r> --task <t> --brief "..."
            ```

            ---

            *You are {agentName}. Claim your identity, read the must-reads, then begin your task.*
            """;
    }

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
    /// Generate a mode file for a specific agent.
    /// Mode files contain role-specific guidance with the agent name baked in.
    /// </summary>
    public static string GenerateModeFile(string agentName, string modeName, string? basePath = null,
        List<string>? sourcePaths = null, List<string>? testPaths = null)
    {
        sourcePaths ??= ["src/**"];
        testPaths ??= ["tests/**"];

        var templateName = $"mode-{modeName}.template.md";
        try
        {
            var template = ReadTemplate(templateName, basePath);
            var placeholders = new Dictionary<string, string>
            {
                ["AGENT_NAME"] = agentName,
                ["SOURCE_PATHS"] = FormatPathsList(sourcePaths),
                ["TEST_PATHS"] = FormatPathsList(testPaths),
                ["SOURCE_AND_TEST_PATHS"] = FormatPathsList([..sourcePaths, ..testPaths])
            };
            var result = ReplacePlaceholders(template, placeholders);
            return ResolveIncludes(result, basePath);
        }
        catch (FileNotFoundException)
        {
            // Fall back to a basic mode file
            return GenerateFallbackModeFile(agentName, modeName, sourcePaths, testPaths);
        }
    }

    public static string FormatPathsList(List<string> paths) =>
        string.Join(", ", paths.Select(p => $"`{p}`"));

    internal static string GenerateFallbackModeFile(string agentName, string modeName,
        List<string> sourcePaths, List<string> testPaths)
    {
        var formattedSource = FormatPathsList(sourcePaths);
        var formattedTests = FormatPathsList(testPaths);
        var formattedAll = FormatPathsList([..sourcePaths, ..testPaths]);

        var roleDescription = modeName switch
        {
            "code-writer" => "implement code",
            "reviewer" => "review code and provide feedback",
            "co-thinker" => "think through problems collaboratively",
            "planner" => "design implementation plans",
            "docs-writer" => "write documentation",
            "test-writer" => "write tests and report issues",
            _ => "complete your assigned work"
        };

        var canEdit = modeName switch
        {
            "code-writer" => $"{formattedSource}, {formattedTests}",
            "reviewer" => $"`dydo/agents/{agentName}/**` (workspace only)",
            "co-thinker" => $"`dydo/agents/{agentName}/**`, `dydo/project/decisions/**`",
            "planner" => $"`dydo/agents/{agentName}/**`, `dydo/project/tasks/**`",
            "docs-writer" => "`dydo/**` (except agents/)",
            "test-writer" => $"`dydo/agents/{agentName}/**`, {formattedTests}, `dydo/project/pitfalls/**`",
            _ => "(check with dydo agent status)"
        };

        return $"""
            ---
            agent: {agentName}
            mode: {modeName}
            ---

            # {agentName} — {char.ToUpper(modeName[0])}{modeName[1..].Replace("-", " ")}

            You are **{agentName}**, working as a **{modeName}**. Your job: {roleDescription}.

            ---

            ## Must-Reads

            1. [about.md](../../../understand/about.md) — What this project is
            2. [architecture.md](../../../understand/architecture.md) — Codebase structure

            ---

            ## Set Role

            ```bash
            dydo agent role {modeName} --task <task-name>
            ```

            ---

            ## Verify

            ```bash
            dydo agent status
            ```

            You can edit: {canEdit}

            ---

            ## Complete

            When done:

            ```bash
            dydo agent release
            ```
            """;
    }

    /// <summary>
    /// Get the list of available mode names.
    /// </summary>
    public static IReadOnlyList<string> GetModeNames()
    {
        return RoleDefinitionService.GetBaseRoleDefinitions()
            .Select(r => r.Name)
            .ToList();
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
            | `dydo init <integration>` | Initialize project (claude, none) |
            | `dydo init <int> --join` | Join existing project |
            | `dydo whoami` | Show current agent identity |

            ## Documentation Commands

            | Command | Description |
            |---------|-------------|
            | `dydo check [path]` | Validate docs |
            | `dydo fix [path]` | Auto-fix issues |
            | `dydo index [path]` | Regenerate index |
            | `dydo graph <file>` | Show link graph |

            ## Agent Commands

            | Command | Description |
            |---------|-------------|
            | `dydo agent claim auto\|<name>` | Claim agent |
            | `dydo agent release` | Release agent |
            | `dydo agent status [name]` | Show status |
            | `dydo agent list [--free] [--all]` | List agents |
            | `dydo agent role <role>` | Set role |
            | `dydo agent new <name> <human>` | Create agent |
            | `dydo agent rename <old> <new>` | Rename agent |
            | `dydo agent remove <name>` | Remove agent |
            | `dydo agent reassign <name> <human>` | Reassign agent |

            ## Task Commands

            | Command | Description |
            |---------|-------------|
            | `dydo task create <name>` | Create task |
            | `dydo task ready-for-review <name>` | Mark ready for review |
            | `dydo task approve <name>` | Approve task |
            | `dydo task reject <name>` | Reject task |
            | `dydo task list` | List tasks |

            ## Workflow Commands

            | Command | Description |
            |---------|-------------|
            | `dydo dispatch` | Dispatch work to another agent |
            | `dydo inbox list` | List agents with inbox items |
            | `dydo inbox show` | Show current agent's inbox |
            | `dydo inbox clear` | Clear processed inbox items |
            | `dydo guard` | Check permissions (for hooks) |
            | `dydo clean <agent>` | Clean agent workspace |
            | `dydo workspace init` | Initialize agent workspaces |
            | `dydo workspace check` | Verify workflow before session end |
            | `dydo review complete <task>` | Complete a code review |

            ## Audit Commands

            | Command | Description |
            |---------|-------------|
            | `dydo audit` | Generate activity replay visualization |
            | `dydo audit --list` | List available sessions |
            | `dydo audit --session <id>` | Show details for a session |

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
            | `planner` | `tasks/**`, agent workspace | Design implementation |
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
