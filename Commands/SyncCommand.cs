namespace DynaDocs.Commands;

using System.CommandLine;
using System.Text;
using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Compiles dydo role definitions into native Claude Code artifacts (Decision 024):
/// a <c>.claude/agents/&lt;role&gt;.md</c> sub-agent definition (identity + tool profile)
/// and a <c>.claude/skills/&lt;role&gt;/SKILL.md</c> with the role's working methodology.
///
/// The role JSON supplies the metadata (name, description, permission shape → tool
/// profile). The mode template supplies the methodology prose, minus the old-runtime
/// orchestration sections (claim / wait / dispatch / release) which the native model
/// replaces.
///
/// Two emission shapes (Decision 024 native pivot):
/// - Worker roles (code-writer, reviewer, test-writer, docs-writer) emit BOTH an agent
///   definition and a skill — they are spawned as typed sub-agents.
/// - Skill-only roles (planner) emit a skill but NO agent: planner is a methodology the
///   orchestrator/co-thinker applies in their own thread, not a claimable identity.
/// </summary>
public static partial class SyncCommand
{
    // Mode-file ## sections that are old-runtime scaffolding, not timeless methodology.
    private static readonly HashSet<string> OrchestrationSections = new(StringComparer.OrdinalIgnoreCase)
    {
        "Must-Reads", "Set Role", "Register General Wait", "Verify", "Complete",
        "Read the Plan or Brief First",
    };

    // Tier-2 worker roles (Decision 024): spawned by orchestrators/workflows to do scoped
    // task work — they emit BOTH a native sub-agent and a skill. Tier-1 roles (orchestrator,
    // co-thinker) are named terminal agents, not sub-agents, so they are not synced here.
    private static readonly string[] WorkerRoles = ["code-writer", "reviewer", "test-writer", "docs-writer"];

    // Skill-only roles (Decision 024): a methodology the Tier-1 agent applies in its own
    // thread (planner = the orchestrator's planning discipline). They emit a skill but NO
    // agent — there is no sub-agent to spawn and no claimable identity. Sourced from the
    // single SkillOnlyRoles set on RoleDefinitionService so the sync emitter and the
    // claimable-surface filters can never drift apart.
    private static readonly HashSet<string> SkillOnlyRoles = RoleDefinitionService.SkillOnlyRoles;

    public static Command Create()
    {
        var command = new Command("sync", "Compile dydo roles into native .claude/ agents and skills");
        command.SetAction(_ => Execute());
        return command;
    }

    private static int Execute()
    {
        var projectRoot = PathUtils.FindProjectRoot() ?? Environment.CurrentDirectory;
        var baseRoles = RoleDefinitionService.GetBaseRoleDefinitions();

        var workerRoles = baseRoles.Where(r => WorkerRoles.Contains(r.Name)).ToList();
        foreach (var role in workerRoles)
            SyncRole(role, projectRoot);

        var skillOnlyRoles = baseRoles.Where(r => SkillOnlyRoles.Contains(r.Name)).ToList();
        foreach (var role in skillOnlyRoles)
            SyncSkillOnlyRole(role, projectRoot);

        Console.WriteLine($"Synced {workerRoles.Count} worker role(s) to .claude/ (agents + skills): {string.Join(", ", workerRoles.Select(r => r.Name))}");
        Console.WriteLine($"Synced {skillOnlyRoles.Count} skill-only role(s) to .claude/ (skills only): {string.Join(", ", skillOnlyRoles.Select(r => r.Name))}");
        return ExitCodes.Success;
    }

    internal static void SyncRole(RoleDefinition role, string projectRoot)
    {
        var agentDir = Path.Combine(projectRoot, ".claude", "agents");
        Directory.CreateDirectory(agentDir);
        File.WriteAllText(Path.Combine(agentDir, $"{role.Name}.md"), BuildAgent(role, ExtractMustReads(role, projectRoot)));

        WriteSkill(role, projectRoot);
    }

    /// <summary>
    /// Emits only the skill for a role, never an agent. Decision 024: planner is a
    /// methodology the Tier-1 agent applies, not a spawnable sub-agent.
    /// </summary>
    internal static void SyncSkillOnlyRole(RoleDefinition role, string projectRoot) =>
        WriteSkill(role, projectRoot);

    private static void WriteSkill(RoleDefinition role, string projectRoot)
    {
        var skillDir = Path.Combine(projectRoot, ".claude", "skills", role.Name);
        Directory.CreateDirectory(skillDir);
        File.WriteAllText(Path.Combine(skillDir, "SKILL.md"), BuildSkill(role, ExtractMethodology(role, projectRoot)));
    }

    /// <summary>
    /// The native sub-agent definition: identity + the tool profile derived from the
    /// role's permission shape. A role that can only write its own workspace is read-only
    /// for the codebase, so it gets no Edit/Write — that is how "reviewers don't write
    /// code" becomes natively enforced rather than guard-RBAC enforced.
    /// </summary>
    private static string BuildAgent(RoleDefinition role, List<string> mustReads)
    {
        var readOnly = IsReadOnlyRole(role);
        var tools = readOnly
            ? "Read, Grep, Glob, Bash"
            : "Read, Grep, Glob, Bash, Edit, Write";
        var stance = readOnly
            ? "You are read-only: you assess and report, you do not modify the project's files."
            : "You produce and modify the project's files as your task requires.";
        var descriptionSuffix = readOnly ? " Use to assess changes without modifying the project." : "";

        var contextBlock = mustReads.Count == 0 ? "" :
            "\n\nRead these for project context before working:\n"
            + string.Join('\n', mustReads.Select(p => $"- {p}")) + "\n";

        return $"""
            ---
            name: {role.Name}
            description: {role.Description}{descriptionSuffix}
            tools: {tools}
            model: inherit
            ---

            You are a **{role.Name}**. {role.Description} {stance} Your methodology lives in
            the `{role.Name}` skill; follow it.
            {contextBlock}
            """;
    }

    private static string BuildSkill(RoleDefinition role, string methodology) => $"""
        ---
        name: {role.Name}
        description: {role.Description} The methodology, standards, and checklist for working as a {role.Name}.
        ---

        {methodology}
        """;

    /// <summary>
    /// Reads the role's mode template, resolves include tags, strips the frontmatter and the
    /// old-runtime orchestration sections, and de-personalizes the {{AGENT_NAME}} prose —
    /// leaving the timeless methodology (mindset, work steps, checklist, out-of-scope).
    /// </summary>
    internal static string ExtractMethodology(RoleDefinition role, string projectRoot)
    {
        var raw = TemplateGenerator.ReadBuiltInTemplate(role.TemplateFile);
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

    /// <summary>Read-only iff the role can write nothing but its own workspace (only {self}
    /// paths) — it touches no source/tests/docs, so it needs no Edit/Write tools.</summary>
    private static bool IsReadOnlyRole(RoleDefinition role) =>
        role.WritablePaths.All(p => p.Contains("{self}"));

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
    /// The role's static must-reads, taken from the [links] in the mode template's
    /// "## Must-Reads" section (normalized to dydo-relative paths) so each role points at
    /// its own context. Conditional must-reads are task-runtime and left to the workflow.
    /// </summary>
    internal static List<string> ExtractMustReads(RoleDefinition role, string projectRoot)
    {
        var template = TemplateGenerator.ResolveIncludes(
            TemplateGenerator.ReadBuiltInTemplate(role.TemplateFile), projectRoot);

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

    private static string StripFrontmatter(string content)
    {
        var match = FrontmatterRegex().Match(content);
        return match.Success ? content[match.Length..] : content;
    }

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
        content = content.Replace($"You are **{{{{AGENT_NAME}}}}**, working as a **{roleName}**.",
            $"You are working as a **{roleName}**.");
        content = content.Replace("{{AGENT_NAME}}", "you");
        return content;
    }

    [GeneratedRegex(@"\A---\r?\n.*?\r?\n---\r?\n", RegexOptions.Singleline)]
    private static partial Regex FrontmatterRegex();

    [GeneratedRegex(@"^## (.+)$", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^(\s*)(\d+)\. (.*)$")]
    private static partial Regex OrderedItemRegex();

    [GeneratedRegex(@"^## Must-Reads\b.*?(?=^## |\z)", RegexOptions.Singleline | RegexOptions.Multiline)]
    private static partial Regex MustReadsSectionRegex();

    [GeneratedRegex(@"\[[^\]]*\]\(([^)]+)\)")]
    private static partial Regex LinkTargetRegex();
}
