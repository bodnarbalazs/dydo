namespace DynaDocs.Commands;

using System.CommandLine;
using System.Text;
using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Compiles dydo roles into native agent artifacts (Decision 024):
/// Claude Code <c>.claude/agents/&lt;role&gt;.md</c> / <c>.claude/skills/&lt;role&gt;/SKILL.md</c>
/// and Codex <c>.codex/agents/&lt;role&gt;.toml</c> / <c>.agents/skills/&lt;role&gt;/SKILL.md</c>.
///
/// The skill template IS the role: its frontmatter supplies the metadata (description,
/// emit shape, invocation policy, read-only → tool profile) and its body supplies the methodology prose,
/// minus the old-runtime orchestration sections (claim / wait / dispatch / release)
/// which the native model replaces. Roles are discovered by enumerating
/// skill-*.template.md — shipped templates plus project-local
/// dydo/_system/templates/ ones, which is how custom roles compile.
///
/// Two emission shapes (Decision 024 native pivot):
/// - <c>emit: agent</c> roles (the workers: code-writer, reviewer, test-writer,
///   docs-writer) emit BOTH an agent definition and a skill — they are spawned as
///   typed sub-agents.
/// - <c>emit: skill</c> roles emit a skill but NO agent: planner is a methodology the
///   orchestrator/co-thinker applies in their own thread, and the Tier-1 manager modes
///   (orchestrator, co-thinker, chief-of-staff — Decision 026) are named terminal
///   identities, never spawnable sub-agents.
/// </summary>
public static partial class SyncCommand
{
    // Skill-template ## sections that are old-runtime scaffolding, not timeless methodology.
    private static readonly HashSet<string> OrchestrationSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "Must-Reads", "Set Role", "Register General Wait", "Verify", "Complete",
        "Read the Plan or Brief First",
    };

    // Framework roles retired during the native-runtime pivot. Sync reconciles only these
    // explicitly-owned filenames when the role is absent; this is deliberately not a generic
    // output-directory cleaner.
    private static readonly string[] RetiredManagedRoles = ["sprint-auditor"];

    // Vendor key used when compiling Claude-native artifacts (Decision 028 §2). A future
    // Codex target reads a different vendor key from the same tiers map; the role → tier
    // section never changes per vendor.
    private const string ClaudeModelVendor = "anthropic";
    private const string OpenAiModelVendor = "openai";

    public static Command Create()
    {
        var command = new Command("sync", "Compile dydo roles into native agent and skill artifacts");
        command.SetAction(_ => Execute());
        return command;
    }

    internal static int Execute(string? projectRoot = null)
    {
        projectRoot ??= PathUtils.FindProjectRoot() ?? Environment.CurrentDirectory;
        var roles = RoleDefinitionService.DiscoverRoles(projectRoot);
        WarnAboutLegacyModeTemplates(projectRoot);
        CleanRetiredRoleArtifacts(projectRoot, roles);
        var config = new ConfigService().LoadConfig(projectRoot);
        var models = config?.Models;
        var (emitClaude, emitCodex) = ResolveIntegrationTargets(config?.Integrations);
        var (workerRoles, skillOnlyRoles) =
            SyncDiscoveredRoles(roles, projectRoot, models, emitClaude, emitCodex);

        if (emitCodex)
            WriteCodexHooks(projectRoot);

        var workflows = emitClaude ? SyncWorkflows(projectRoot) : 0;
        PrintSyncSummary(workerRoles, skillOnlyRoles, workflows, emitClaude, emitCodex);
        return ExitCodes.Success;
    }

    private static void WarnAboutLegacyModeTemplates(string projectRoot)
    {
        foreach (var legacyTemplate in TemplateGenerator.GetProjectLegacyModeTemplateNames(projectRoot))
        {
            var skillTemplate = "skill-" + legacyTemplate["mode-".Length..];
            Console.Error.WriteLine("Warning: dydo sync ignores "
                + $"dydo/_system/templates/{legacyTemplate}; rename it to "
                + $"dydo/_system/templates/{skillTemplate}.");
        }
    }

    /// <summary>
    /// Emit only integrations recorded in dydo.json. A project with neither hook-wired
    /// integration recorded (legacy config, or integration "none") keeps the old emit-everything
    /// behavior rather than silently emitting nothing.
    /// </summary>
    internal static (bool EmitClaude, bool EmitCodex) ResolveIntegrationTargets(
        Dictionary<string, bool>? integrations)
    {
        var anyRecorded = integrations != null
            && (integrations.GetValueOrDefault("claude") || integrations.GetValueOrDefault("codex"));
        return (
            !anyRecorded || integrations!.GetValueOrDefault("claude"),
            !anyRecorded || integrations!.GetValueOrDefault("codex"));
    }

    private static (List<RoleDefinition> WorkerRoles, List<RoleDefinition> SkillOnlyRoles)
        SyncDiscoveredRoles(
            IReadOnlyCollection<RoleDefinition> roles,
            string projectRoot,
            ModelsConfig? models,
            bool emitClaude,
            bool emitCodex)
    {
        var workerRoles = roles.Where(role => role.EmitAgent).ToList();
        foreach (var role in workerRoles)
        {
            if (emitClaude) SyncRole(role, projectRoot, models);
            if (emitCodex) SyncCodexRole(role, projectRoot, models);
        }

        var skillOnlyRoles = roles.Where(role => !role.EmitAgent).ToList();
        foreach (var role in skillOnlyRoles)
        {
            if (emitClaude) SyncSkillOnlyRole(role, projectRoot);
            if (emitCodex) SyncCodexSkill(role, projectRoot);
        }

        return (workerRoles, skillOnlyRoles);
    }

    private static void PrintSyncSummary(
        IReadOnlyCollection<RoleDefinition> workerRoles,
        IReadOnlyCollection<RoleDefinition> skillOnlyRoles,
        int workflows,
        bool emitClaude,
        bool emitCodex)
    {
        if (emitClaude)
        {
            Console.WriteLine($"Synced {workerRoles.Count} worker role(s) to .claude/ (agents + skills): {string.Join(", ", workerRoles.Select(r => r.Name))}");
            Console.WriteLine($"Synced {skillOnlyRoles.Count} skill-only role(s) to .claude/ (skills only): {string.Join(", ", skillOnlyRoles.Select(r => r.Name))}");
            Console.WriteLine($"Synced {workflows} workflow(s) to .claude/workflows.");
        }
        if (emitCodex)
            Console.WriteLine($"Synced Codex artifacts to .agents/skills and .codex/agents.");
        if (!emitClaude || !emitCodex)
            Console.WriteLine($"Skipped {(emitClaude ? "Codex" : "Claude")} artifacts — not recorded in dydo.json integrations (add it with 'dydo init <integration> --join').");
    }

    /// <summary>
    /// Removes compiler-owned files for allowlisted retired roles. A project-local skill template
    /// makes the role active again and suppresses cleanup. Skill folders are removed only when
    /// empty so project-owned sibling resources survive.
    /// </summary>
    internal static int CleanRetiredRoleArtifacts(
        string projectRoot,
        IReadOnlyCollection<RoleDefinition> activeRoles)
    {
        var activeNames = activeRoles
            .Select(role => role.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var removed = 0;

        foreach (var roleName in RetiredManagedRoles)
        {
            if (activeNames.Contains(roleName))
                continue;

            var roleRemoved = 0;
            var files = new[]
            {
                Path.Combine(projectRoot, ".claude", "agents", $"{roleName}.md"),
                Path.Combine(projectRoot, ".claude", "skills", roleName, "SKILL.md"),
                Path.Combine(projectRoot, ".codex", "agents", $"{roleName}.toml"),
                Path.Combine(projectRoot, ".agents", "skills", roleName, "SKILL.md"),
            };

            foreach (var file in files)
            {
                if (!File.Exists(file))
                    continue;

                File.Delete(file);
                removed++;
                roleRemoved++;

                var parent = Path.GetDirectoryName(file);
                if (parent != null
                    && Directory.Exists(parent)
                    && !Directory.EnumerateFileSystemEntries(parent).Any())
                {
                    Directory.Delete(parent);
                }
            }

            if (roleRemoved > 0)
                Console.WriteLine($"Removed retired role artifacts for '{roleName}'.");
        }

        return removed;
    }

    /// <summary>
    /// Workflow harnesses (Templates/workflow-&lt;name&gt;.js) → .claude/workflows/&lt;name&gt;.js.
    /// Claude-only; a codex emit path is added when codex grows an equivalent runner.
    /// </summary>
    internal static int SyncWorkflows(string projectRoot)
    {
        var count = 0;
        foreach (var (fileName, content) in TemplateGenerator.GetWorkflowScripts())
        {
            var workflowDir = Path.Combine(projectRoot, ".claude", "workflows");
            Directory.CreateDirectory(workflowDir);
            WriteLf(Path.Combine(workflowDir, fileName), content);
            count++;
        }
        return count;
    }

    internal static void SyncRole(RoleDefinition role, string projectRoot, ModelsConfig? models = null)
    {
        var agentDir = Path.Combine(projectRoot, ".claude", "agents");
        Directory.CreateDirectory(agentDir);
        WriteLf(Path.Combine(agentDir, $"{role.Name}.md"), BuildAgent(role, ExtractMustReads(role, projectRoot), models));

        WriteSkill(role, projectRoot);
    }

    /// <summary>
    /// Emits only the skill for a role, never an agent. Decision 024: planner is a
    /// methodology the Tier-1 agent applies, not a spawnable sub-agent.
    /// </summary>
    internal static void SyncSkillOnlyRole(RoleDefinition role, string projectRoot) =>
        WriteSkill(role, projectRoot);

    internal static void SyncCodexRole(RoleDefinition role, string projectRoot, ModelsConfig? models = null)
    {
        SyncCodexSkill(role, projectRoot);

        var agentDir = Path.Combine(projectRoot, ".codex", "agents");
        Directory.CreateDirectory(agentDir);
        WriteLf(Path.Combine(agentDir, $"{role.Name}.toml"),
            BuildCodexAgent(role, ExtractMustReads(role, projectRoot), models));
    }

    internal static void SyncCodexSkill(RoleDefinition role, string projectRoot)
    {
        var skillDir = Path.Combine(projectRoot, ".agents", "skills", role.Name);
        Directory.CreateDirectory(skillDir);
        WriteLf(
            Path.Combine(skillDir, "SKILL.md"),
            BuildSkill(role, ExtractMethodology(role, projectRoot), emitClaudePolicy: false));
        WriteCodexInvocationPolicy(role, skillDir);
        WriteSkillResources(role, skillDir);
    }

    internal static void WriteCodexHooks(string projectRoot)
        => InitCommand.ConfigureCodexHooks(projectRoot);

    private static void WriteSkill(RoleDefinition role, string projectRoot)
    {
        var skillDir = Path.Combine(projectRoot, ".claude", "skills", role.Name);
        Directory.CreateDirectory(skillDir);
        WriteLf(
            Path.Combine(skillDir, "SKILL.md"),
            BuildSkill(role, ExtractMethodology(role, projectRoot), emitClaudePolicy: true));
        WriteSkillResources(role, skillDir);
    }

    private static void WriteCodexInvocationPolicy(RoleDefinition role, string skillDir)
    {
        var agentsDir = Path.Combine(skillDir, "agents");
        var policyFile = Path.Combine(agentsDir, "openai.yaml");

        if (role.ExplicitInvocation)
        {
            Directory.CreateDirectory(agentsDir);
            WriteLf(policyFile, "policy:\n  allow_implicit_invocation: false\n");
            return;
        }

        if (!File.Exists(policyFile))
            return;

        File.Delete(policyFile);
        if (!Directory.EnumerateFileSystemEntries(agentsDir).Any())
            Directory.Delete(agentsDir);
    }

    /// <summary>
    /// Skill resource templates (<role>-resource-<name>.template.md) compile into the
    /// skill folder's resources/ (DR-039 review-target subskills; DR-042).
    /// </summary>
    private static void WriteSkillResources(RoleDefinition role, string skillDir)
    {
        foreach (var (fileName, content) in TemplateGenerator.GetSkillResources(role.Name))
        {
            var resourceDir = Path.Combine(skillDir, "resources");
            Directory.CreateDirectory(resourceDir);
            WriteLf(Path.Combine(resourceDir, fileName), content);
        }
    }

    /// <summary>
    /// .claude/ artifacts must be LF regardless of platform: Claude Code's Workflow/agent
    /// permission handling rejects files whose CR bytes "would be hidden in the approval
    /// dialog". Template sources and C# raw string literals carry CRLF on Windows, so
    /// normalize at the single write boundary instead of chasing every content source.
    /// </summary>
    private static void WriteLf(string path, string content) =>
        File.WriteAllText(path, content.Replace("\r\n", "\n").Replace("\r", "\n"));

    /// <summary>
    /// The native sub-agent definition: identity + the tool profile derived from the
    /// role's permission shape. A role that can write nothing is read-only for the codebase,
    /// so it gets no Edit/Write — that is how "reviewers don't write code" becomes natively
    /// enforced rather than guard-RBAC enforced. The allowlist also
    /// deliberately never includes the Agent tool: worker roles cannot dispatch subagents
    /// (Decision 026 requires this natively for the reviewer's merge-sprint audit).
    /// </summary>
    private static string BuildAgent(RoleDefinition role, List<string> mustReads, ModelsConfig? models = null)
    {
        var readOnly = IsReadOnlyRole(role);
        var tools = readOnly
            ? "Read, Grep, Glob, Bash"
            : "Read, Grep, Glob, Bash, Edit, Write";
        var stance = readOnly
            ? "You are read-only: you assess and report, you do not modify the project's files."
            : "You produce and modify the project's files as your task requires.";
        var contextBlock = mustReads.Count == 0 ? "" :
            "\n\nRead these for project context before working:\n"
            + string.Join('\n', mustReads.Select(p => $"- {p}")) + "\n";

        // Decision 028: role → tier → concrete model, bound here by the compiler so
        // workflows stay tier-blind. An unresolved role emits `model: inherit` — the
        // explicit no-silent-downgrade spelling (an OMITTED model would fall back to
        // Claude Code's default subagent model, not the session model).
        var (model, effort) = ResolveModel(models, role.Name);
        var effortLine = model != null && effort != null ? $"\neffort: {effort}" : "";

        return $"""
            ---
            name: {role.Name}
            description: {role.Description}
            tools: {tools}
            model: {model ?? "inherit"}{effortLine}
            ---

            You are {Article(role.Name)} **{role.Name}**. {role.Description} {stance} Your methodology lives in
            the `{role.Name}` skill; follow it.
            {contextBlock}
            """;
    }

    /// <summary>
    /// Resolves role → tier → concrete model for the compile vendor (Decision 028).
    /// Null model means "no binding" — unmapped role, absent models section, or a tier
    /// missing from the vendor map — and the caller emits <c>model: inherit</c> so the
    /// agent runs on the session model instead of silently downgrading.
    /// </summary>
    internal static (string? Model, string? Effort) ResolveModel(ModelsConfig? models, string roleName)
        => ResolveModel(models, roleName, ClaudeModelVendor);

    internal static (string? Model, string? Effort) ResolveModel(ModelsConfig? models, string roleName, string vendor)
    {
        if (models == null || !models.Roles.TryGetValue(roleName, out var tier))
            return (null, null);
        if (!models.Tiers.TryGetValue(vendor, out var vendorTiers)
            || !vendorTiers.TryGetValue(tier, out var model))
            return (null, null);
        return (model, models.Efforts.GetValueOrDefault(roleName));
    }

    private static string BuildCodexAgent(RoleDefinition role, List<string> mustReads, ModelsConfig? models)
    {
        // No `tools` field: codex's agent `tools` is a ToolsToml struct of codex-defined
        // toggles (view_image, web_search) — NOT file/shell tool names. Claude's tool names
        // are category-wrong here and have no valid codex representation; codex grants
        // apply_patch/shell/read intrinsically and inherits toggles from the parent when the
        // field is omitted. Read-only capability is a separate concern (issue 0272,
        // sandbox_mode). See issue 0271.
        var readOnly = IsReadOnlyRole(role);
        var stance = readOnly
            ? "You are read-only: assess and report without modifying project files."
            : "You produce and modify the project's files as your task requires.";
        var contextBlock = mustReads.Count == 0 ? "" :
            "\n\nRead these for project context before working:\n"
            + string.Join('\n', mustReads.Select(p => $"- {p}"));
        var (model, _) = ResolveModel(models, role.Name, OpenAiModelVendor);

        return $""""
            name = "{EscapeToml(role.Name)}"
            description = "{EscapeToml(role.Description)}"
            model = "{EscapeToml(model ?? "gpt-5.6-terra")}"{(readOnly ? "\nsandbox_mode = \"read-only\"" : "")}

            developer_instructions = """
            You are {Article(role.Name)} **{role.Name}**. {role.Description} {stance} Your methodology lives in the `{role.Name}` skill; follow it.{contextBlock}
            """
            """";
    }

    private static string EscapeToml(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string BuildSkill(
        RoleDefinition role,
        string methodology,
        bool emitClaudePolicy)
    {
        var invocationPolicy = emitClaudePolicy && role.ExplicitInvocation
            ? "\ndisable-model-invocation: true"
            : "";

        return $"""
        ---
        name: {role.Name}
        description: {role.Description}{invocationPolicy}
        ---

        {methodology}
        """;
    }

    private static string Article(string noun) =>
        "aeiou".Contains(char.ToLowerInvariant(noun[0])) ? "an" : "a";

    /// <summary>
    /// Reads the role's skill template, resolves include tags, strips the frontmatter and the
    /// old-runtime orchestration sections, and de-personalizes the {{AGENT_NAME}} prose —
    /// leaving the timeless methodology (mindset, work steps, checklist, out-of-scope).
    /// </summary>
    internal static string ExtractMethodology(RoleDefinition role, string projectRoot)
    {
        var raw = TemplateGenerator.ReadTemplate(role.TemplateFile, projectRoot);
        // Resolve includes against the project root so project-local template-additions
        // overrides are honored regardless of the CWD dydo was invoked from.
        var resolved = TemplateGenerator.ResolveIncludes(raw, projectRoot);

        var body = StripFrontmatter(resolved);
        body = DropOrchestrationSections(body);
        body = Depersonalize(body, role.Name);
        body = RenumberOrderedLists(body);

        // Strip any horizontal rule left dangling at the end after dropping a trailing section.
        body = Regex.Replace(body, @"(\s*\n---\s*)+\s*$", "\n");
        return body.Trim() + "\n";
    }

    /// <summary>A role with frontmatter <c>read-only: true</c> needs no Edit/Write tools.</summary>
    private static bool IsReadOnlyRole(RoleDefinition role) =>
        role.ReadOnly;

    /// <summary>
    /// Renumbers each run of consecutive ordered-list items (1., 2., …) so that concatenating
    /// a template section with an included continuation doesn't leave duplicate or jumped
    /// numbers in the compiled skill.
    /// </summary>
    internal static string RenumberOrderedLists(string content)
    {
        var lines = content.Split('\n');
        var n = 0;
        var inFence = false;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("```"))
            {
                inFence = !inFence;
                continue;
            }
            if (inFence)
                continue; // never renumber or reset on a literal "N." or "# comment" inside a code fence

            var m = OrderedItemRegex().Match(lines[i]);
            if (m.Success)
                lines[i] = $"{m.Groups[1].Value}{++n}. {m.Groups[3].Value}";
            else if (lines[i].StartsWith('#'))
                n = 0; // a heading starts a fresh list; prose/blank between items don't
        }
        return string.Join('\n', lines);
    }

    /// <summary>
    /// The role's static must-reads, taken from the [links] in the skill template's
    /// "## Must-Reads" section (normalized to dydo-relative paths) so each role points at
    /// its own context. Conditional must-reads are task-runtime and left to the workflow.
    /// </summary>
    internal static List<string> ExtractMustReads(RoleDefinition role, string projectRoot)
    {
        var template = TemplateGenerator.ResolveIncludes(
            TemplateGenerator.ReadTemplate(role.TemplateFile, projectRoot), projectRoot);

        var section = MustReadsSectionRegex().Match(template);
        if (!section.Success)
            return [];

        return LinkTargetRegex().Matches(section.Value)
            .Select(m => NormalizeMustReadTarget(m.Groups[1].Value))
            .Where(p => p.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            .Distinct()
            .ToList();
    }

    private static string NormalizeMustReadTarget(string target)
    {
        var path = target.Replace('\\', '/');
        path = Regex.Replace(path, @"^(\.\./)+", "");   // strip the ../../.. climb out of modes/
        return path.StartsWith("dydo/", StringComparison.OrdinalIgnoreCase) ? path : "dydo/" + path;
    }

    // Delegate to the shared frontmatter helper so the opener tolerance and empty-block handling match every
    // other reader — no strict-regex divergence (finding 8).
    private static string StripFrontmatter(string content) => FrontmatterParser.StripFrontmatter(content);

    private static string DropOrchestrationSections(string content)
    {
        // Split on ## headings, keeping the leading # title block, and drop any section
        // whose heading is in OrchestrationSections.
        var parts = Regex.Split(content, @"(?=^## )", RegexOptions.Multiline);
        var kept = new StringBuilder();
        foreach (var part in parts)
        {
            var heading = HeadingRegex().Match(part);
            if (heading.Success && OrchestrationSections.Contains(heading.Groups[1].Value.Trim()))
                continue;
            kept.Append(part);
        }
        // Collapse the horizontal rules left dangling by removed sections.
        return Regex.Replace(kept.ToString(), @"(\n---\s*){2,}", "\n---\n");
    }

    private static string Depersonalize(string content, string roleName)
    {
        content = content.Replace($"{{{{AGENT_NAME}}}} — ", "");
        foreach (var article in new[] { "a", "an" })
            content = content.Replace($"You are **{{{{AGENT_NAME}}}}**, working as {article} **{roleName}**.",
                $"You are working as {article} **{roleName}**.");
        content = content.Replace("{{AGENT_NAME}}", "you");
        return content;
    }

    [GeneratedRegex(@"^## (.+)$", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^(\s*)(\d+)\. (.*)$")]
    private static partial Regex OrderedItemRegex();

    [GeneratedRegex(@"^## Must-Reads\b.*?(?=^## |\z)", RegexOptions.Singleline | RegexOptions.Multiline)]
    private static partial Regex MustReadsSectionRegex();

    [GeneratedRegex(@"\[[^\]]*\]\(([^)]+)\)")]
    private static partial Regex LinkTargetRegex();
}
