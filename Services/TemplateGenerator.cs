namespace DynaDocs.Services;

using System.Reflection;

/// <summary>
/// Generates documentation files by reading templates from the Templates/ folder
/// and replacing placeholders like {{AGENT_NAME}}, {{PROJECT_NAME}}.
/// </summary>
public static class TemplateGenerator
{
    private static string? _templatesPath;

    /// <summary>
    /// Gets the path to the Templates folder (embedded with the assembly).
    /// </summary>
    private static string GetTemplatesPath()
    {
        if (_templatesPath != null)
            return _templatesPath;

        // Try to find Templates folder relative to the assembly
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (assemblyDir != null)
        {
            // In development: look for Templates in project root
            var devPath = FindTemplatesFolder(assemblyDir);
            if (devPath != null)
            {
                _templatesPath = devPath;
                return _templatesPath;
            }
        }

        // Fallback: look from current directory up
        var fallbackPath = FindTemplatesFolder(Environment.CurrentDirectory);
        if (fallbackPath != null)
        {
            _templatesPath = fallbackPath;
            return _templatesPath;
        }

        throw new InvalidOperationException("Could not find Templates folder");
    }

    private static string? FindTemplatesFolder(string startDir)
    {
        var current = startDir;
        while (current != null)
        {
            var templatesPath = Path.Combine(current, "Templates");
            if (Directory.Exists(templatesPath) && File.Exists(Path.Combine(templatesPath, "agent-workflow.template.md")))
            {
                return templatesPath;
            }

            // Also check if we're in a bin folder and need to go up
            var parentTemplates = Path.Combine(current, "..", "Templates");
            if (Directory.Exists(parentTemplates) && File.Exists(Path.Combine(parentTemplates, "agent-workflow.template.md")))
            {
                return Path.GetFullPath(parentTemplates);
            }

            current = Path.GetDirectoryName(current);
        }
        return null;
    }

    /// <summary>
    /// Reads a template file and returns its content.
    /// </summary>
    private static string ReadTemplate(string templateName)
    {
        var templatesPath = GetTemplatesPath();
        var filePath = Path.Combine(templatesPath, templateName);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Template not found: {templateName}", filePath);
        }

        return File.ReadAllText(filePath);
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

            **Start here:** [dydo/index.md](dydo/index.md)

            ---

            ## Quick Reference

            If you've worked on this project before:

            - **Claim identity:** `dydo agent claim auto`
            - **Check status:** `dydo whoami`
            - **View inbox:** `dydo inbox show`

            For first-time setup, read [dydo/index.md](dydo/index.md).
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

    private static string GenerateJitiIndexMd(List<string> agentNames, string workflowLinks)
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
            dydo agent list         # List all agents
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
    public static string GenerateWorkflowFile(string agentName)
    {
        try
        {
            var template = ReadTemplate("agent-workflow.template.md");
            var placeholders = new Dictionary<string, string>
            {
                ["AGENT_NAME"] = agentName,
                ["AGENT_NAME_LOWER"] = agentName.ToLowerInvariant()
            };
            return ReplacePlaceholders(template, placeholders);
        }
        catch (FileNotFoundException)
        {
            // Fall back to generated content if template not found
            return GenerateFallbackWorkflowFile(agentName);
        }
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
            // Fall back to inline generation if template not found
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
                Check agent status: dydo agent list
                -->
                """;
        }
    }

    private static string GenerateFallbackWorkflowFile(string agentName)
    {
        var lowerName = agentName.ToLowerInvariant();

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
            | `code-writer` | `src/**`, `tests/**` | `dydo/**`, `project/**` |
            | `reviewer` | (read-only) | (all files) |
            | `co-thinker` | `dydo/agents/{agentName}/**`, `dydo/project/decisions/**` | `src/**`, `tests/**` |
            | `docs-writer` | `dydo/**` | `dydo/agents/**`, `src/**` |
            | `interviewer` | `dydo/agents/{agentName}/**` | Everything else |
            | `planner` | `dydo/agents/{agentName}/**`, `dydo/project/tasks/**` | `src/**` |

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

    private static string GenerateFallbackArchitectureMd()
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

    private static string GenerateFallbackWelcomeMd()
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

    private static string GenerateFallbackCodingStandardsMd()
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
    /// How to use docs - comprehensive guide to dydo commands and workflow.
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

    private static string GenerateFallbackHowToUseDocsMd()
    {
        return """
            ---
            area: guides
            type: guide
            ---

            # How to Use DynaDocs

            This guide covers the dydo command-line tool and the agent workflow system.

            ---

            ## Quick Start

            ```bash
            # First time setup
            export DYDO_HUMAN=your_name     # Set your identity
            dydo agent claim auto           # Claim an available agent
            dydo agent role code-writer     # Set your role

            # Check status
            dydo whoami                     # Show current agent identity
            ```

            ---

            ## Environment Setup

            ### DYDO_HUMAN Variable

            The `DYDO_HUMAN` environment variable identifies which human is operating the terminal.
            This determines which agents you can claim.

            ```bash
            # Bash/Zsh
            export DYDO_HUMAN=your_name

            # PowerShell
            $env:DYDO_HUMAN = "your_name"
            ```

            ---

            ## Agent Commands

            ### Claiming an Agent

            ```bash
            dydo agent claim auto      # Claim first available
            dydo agent claim Adele     # Claim specific agent
            ```

            ### Setting Role

            ```bash
            dydo agent role code-writer --task implement-auth
            ```

            ### Releasing

            ```bash
            dydo agent release
            ```

            ---

            ## Command Reference

            | Command | Description |
            |---------|-------------|
            | `dydo whoami` | Show current agent identity |
            | `dydo agent claim <name\|auto>` | Claim an agent |
            | `dydo agent release` | Release current agent |
            | `dydo agent role <role>` | Set role |
            | `dydo check` | Validate docs |
            | `dydo help` | Show help |
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
            return """
                ---
                area: understand
                type: context
                ---

                # About This Project

                > **Fill this in.** This is the first thing AI agents read.

                ---

                ## What We're Building

                *[Describe the project in 2-3 sentences]*

                ---

                ## Tech Stack

                | Layer | Technology |
                |-------|------------|
                | Language | *[e.g., C#, TypeScript]* |
                | Framework | *[e.g., .NET 8, React]* |

                ---

                *See [architecture.md](./architecture.md) for technical structure.*
                """;
        }
    }

    /// <summary>
    /// Generate a mode file for a specific agent.
    /// Mode files contain role-specific guidance with the agent name baked in.
    /// </summary>
    public static string GenerateModeFile(string agentName, string modeName)
    {
        var templateName = $"mode-{modeName}.template.md";
        try
        {
            var template = ReadTemplate(templateName);
            var placeholders = new Dictionary<string, string>
            {
                ["AGENT_NAME"] = agentName,
                ["AGENT_NAME_LOWER"] = agentName.ToLowerInvariant()
            };
            return ReplacePlaceholders(template, placeholders);
        }
        catch (FileNotFoundException)
        {
            // Fall back to a basic mode file
            return GenerateFallbackModeFile(agentName, modeName);
        }
    }

    private static string GenerateFallbackModeFile(string agentName, string modeName)
    {
        var roleDescription = modeName switch
        {
            "code-writer" => "implement code",
            "reviewer" => "review code (read-only)",
            "co-thinker" => "think through problems collaboratively",
            "interviewer" => "gather requirements",
            "planner" => "design implementation plans",
            "docs-writer" => "write documentation",
            _ => "complete your assigned work"
        };

        var canEdit = modeName switch
        {
            "code-writer" => "`src/**`, `tests/**`",
            "reviewer" => "(nothing — read-only)",
            "co-thinker" => $"`dydo/agents/{agentName}/**`, `dydo/project/decisions/**`",
            "interviewer" => $"`dydo/agents/{agentName}/**`",
            "planner" => $"`dydo/agents/{agentName}/**`, `dydo/project/tasks/**`",
            "docs-writer" => "`dydo/**` (except agents/)",
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
        return new[] { "code-writer", "reviewer", "co-thinker", "interviewer", "planner", "docs-writer" };
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

    private static string GenerateFallbackFilesOffLimitsMd()
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
    /// Generate the CLI commands reference document.
    /// Reads from cli-commands.template.md if available.
    /// </summary>
    public static string GenerateCliCommandsMd()
    {
        try
        {
            return ReadTemplate("cli-commands.template.md");
        }
        catch (FileNotFoundException)
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
                | `dydo agent list [--free]` | List agents |
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

                ---

                Run `dydo <command> --help` for detailed usage of each command.
                """;
        }
    }
}
