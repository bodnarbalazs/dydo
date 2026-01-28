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
        var workflowLinks = string.Join("\n", agentNames.Select(name =>
            $"- [{name}](workflows/{name.ToLowerInvariant()}.md)"));

        // Read the index template and customize it
        try
        {
            var template = ReadTemplate("index.template.md");

            // The template has a different structure - generate our JITI-focused version
            return GenerateJitiIndexMd(agentNames, workflowLinks);
        }
        catch (FileNotFoundException)
        {
            // Fall back to generated content if template not found
            return GenerateJitiIndexMd(agentNames, workflowLinks);
        }
    }

    private static string GenerateJitiIndexMd(List<string> agentNames, string workflowLinks)
    {
        return """
            ---
            area: general
            type: entry
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
            ├── workflows/            # Agent workflow files
            │   ├── adele.md
            │   ├── brian.md
            │   └── ...
            ├── understand/           # Core concepts & architecture
            ├── guides/               # How-to guides
            ├── reference/            # API & configuration
            ├── project/              # Decisions, changelog, tasks
            │   ├── tasks/
            │   ├── decisions/
            │   └── changelog/
            └── agents/               # Agent workspaces (gitignored)
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
    /// </summary>
    public static string GenerateArchitectureMd()
    {
        return """
            ---
            area: understand
            type: concept
            ---

            # Architecture Overview

            > **TODO:** This is a template. Replace with your project's actual architecture.

            ---

            ## Project Structure

            ```
            project/
            ├── src/                  # Source code
            │   ├── components/       # UI components
            │   ├── services/         # Business logic
            │   ├── models/           # Data models
            │   └── utils/            # Utilities
            ├── tests/                # Test files
            ├── dydo/                 # Documentation & agent workspaces
            └── ...
            ```

            ---

            ## Key Components

            ### Component A

            *Describe what this component does, its responsibilities, and how it interacts with other components.*

            ### Component B

            *Describe the next key component.*

            ---

            ## Data Flow

            *Describe how data flows through the system. Include diagrams if helpful.*

            ```
            User Input → Component A → Component B → Output
            ```

            ---

            ## Key Decisions

            *Link to decision records in `project/decisions/` for architectural choices.*

            - Why we chose X over Y
            - Why component A is structured this way

            ---

            ## Where to Find Things

            | Looking for... | Location |
            |----------------|----------|
            | API endpoints | `src/api/` |
            | Database models | `src/models/` |
            | Business logic | `src/services/` |
            | Configuration | `src/config/` |
            | Tests | `tests/` |

            ---

            ## Common Patterns

            *Describe patterns used throughout the codebase that new contributors should know.*

            ---

            ## Next Steps

            After understanding the architecture:
            - Read [../guides/coding-standards.md](../guides/coding-standards.md) for code style
            - Read [../guides/how-to-use-docs.md](../guides/how-to-use-docs.md) for workflow commands
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
    /// Reads from docs-system.template.md if available.
    /// </summary>
    public static string GenerateHowToUseDocsMd()
    {
        try
        {
            return ReadTemplate("docs-system.template.md");
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
}
