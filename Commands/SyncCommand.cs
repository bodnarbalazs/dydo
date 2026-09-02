namespace DynaDocs.Commands;

using System.CommandLine;
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
/// emit shape, invocation policy, delegation, read-only → tool profile) and its body supplies
/// the whole methodology — context pointers included.
///
/// Two emission shapes (Decision 024 native pivot):
/// - <c>emit: agent</c> roles emit BOTH an agent definition and a skill — they are spawned as
///   typed sub-agents, and the agent is a thin identity wrapper that preloads its skill
///   (DR 045 §10).
/// - <c>emit: skill</c> roles emit a skill but NO agent: the methodology is one a session
///   applies in its own thread, never a spawnable sub-agent.
/// </summary>
public static partial class SyncCommand
{
    // Framework roles dydo no longer owns. A retired name leaves the shipped template set
    // (TemplateGenerator.GetBuiltInSkillTemplateNames), so it is never discovered, and the
    // sweep below removes whatever it last compiled. This is deliberately not a generic
    // output-directory cleaner.
    internal static readonly string[] RetiredManagedRoles = ["sprint-auditor", "orchestrator", "implementer", "manager", "planner"];

    // Workflow harnesses dydo no longer ships (DR 045: the run-sprint loop became the
    // Issue Captain's completion criterion). Claude is the only host with a workflow surface.
    private static readonly string[] RetiredWorkflows = ["run-sprint.js"];

    // Skill resources retired by rename (DR 045: merge-sprint became merge; the broad plan
    // rubric split into project-plan and issue-plan; the generic planner split into two roles),
    // swept from both hosts' skill folders.
    private static readonly string[] RetiredSkillResources =
    [
        "reviewer/resources/merge-sprint.md",
        "reviewer/resources/plan.md",
        "planner/resources/project.md",
        "planner/resources/issue.md"
    ];

    // Where each host emits a skill: <root>/<role>/SKILL.md, three levels below the project
    // root on both, which is why one link rewrite serves them equally.
    private const string ClaudeSkillRoot = ".claude/skills";
    private const string CodexSkillRoot = ".agents/skills";

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
        var roles = RoleDefinitionService.DiscoverRoles();
        CleanRetiredArtifacts(projectRoot);
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
    /// Removes compiler-owned files dydo no longer emits: allowlisted retired roles, retired
    /// workflow harnesses, and resources retired by rename. Folders are removed only when
    /// empty so project-owned siblings survive.
    /// </summary>
    internal static int CleanRetiredArtifacts(string projectRoot)
    {
        var removed = 0;

        foreach (var roleName in RetiredManagedRoles)
        {
            var roleRemoved = Sweep(
                Path.Combine(projectRoot, ".claude", "agents", $"{roleName}.md"),
                Combine(projectRoot, ClaudeSkillRoot, $"{roleName}/SKILL.md"),
                Path.Combine(projectRoot, ".codex", "agents", $"{roleName}.toml"),
                Combine(projectRoot, CodexSkillRoot, $"{roleName}/SKILL.md"));

            if (roleRemoved > 0)
                Console.WriteLine($"Removed retired role artifacts for '{roleName}'.");
            removed += roleRemoved;
        }

        foreach (var workflow in RetiredWorkflows)
            removed += Sweep(Path.Combine(projectRoot, ".claude", "workflows", workflow));

        foreach (var resource in RetiredSkillResources)
            removed += Sweep(
                Combine(projectRoot, ClaudeSkillRoot, resource),
                Combine(projectRoot, CodexSkillRoot, resource));

        return removed;
    }

    private static int Sweep(params string[] files) => files.Count(DeleteIfPresent);

    private static string Combine(string projectRoot, params string[] relativeSegments) =>
        Path.Combine(projectRoot,
            Path.Combine(relativeSegments.Select(s => s.Replace('/', Path.DirectorySeparatorChar)).ToArray()));

    /// <summary>Deletes a compiler-owned file and the folder it leaves empty. True when it existed.</summary>
    private static bool DeleteIfPresent(string file)
    {
        if (!File.Exists(file))
            return false;

        File.Delete(file);

        var parent = Path.GetDirectoryName(file);
        if (parent != null && Directory.Exists(parent) && !Directory.EnumerateFileSystemEntries(parent).Any())
            Directory.Delete(parent);

        return true;
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
    /// Emits only the skill for a role, never an agent (Decision 024): the role is a methodology
    /// a session applies in its own thread, not a spawnable sub-agent.
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
            BuildSkill(role, CompileSkillBody(role, projectRoot, CodexSkillRoot), emitClaudePolicy: false));
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
            BuildSkill(role, CompileSkillBody(role, projectRoot, ClaudeSkillRoot), emitClaudePolicy: true));
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
    /// skill folder's resources/ (DR-039 review-target subskills; DR-042). Resource bodies are
    /// copied verbatim: they are authored one folder deeper than SKILL.md and already carry the
    /// climbs that resolve from resources/, so the skill-body link rewrite must not reach them.
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
    /// The native sub-agent definition: a thin identity wrapper over the role's skill plus the
    /// tool profile derived from its permission shape. A role that can write nothing is read-only
    /// for the codebase, so it gets no Edit/Write — that is how "reviewers don't write code"
    /// becomes natively enforced rather than guard-RBAC enforced. Every agent carries
    /// <c>skills:</c>, which preloads the skill's full content at startup, and the Skill tool, so
    /// the methodology actually reaches the spawned agent; the Agent tool is granted only to a
    /// role whose frontmatter declares <c>delegates: true</c>, so workers still cannot fan out
    /// (DR 045 §10).
    /// </summary>
    private static string BuildAgent(RoleDefinition role, List<string> mustReads, ModelsConfig? models = null)
    {
        var readOnly = IsReadOnlyRole(role);
        var tools = readOnly
            ? "Read, Grep, Glob, Bash, Skill"
            : "Read, Grep, Glob, Bash, Edit, Write, Skill";
        if (role.Delegates)
            tools += ", Agent";
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
        var model = ResolveModel(models, role.Name);

        return $"""
            ---
            name: {role.Name}
            description: {role.Description}
            tools: {tools}
            skills: [{role.Name}]
            model: {model ?? "inherit"}
            ---

            You are {Article(role.Name)} **{role.Name}**. {role.Description} {stance} Your methodology lives in
            the `{role.Name}` skill; follow it.
            {contextBlock}
            """;
    }

    /// <summary>
    /// Resolves agent → tier → concrete model for the compile vendor (Decision 028).
    /// Null model means "no binding" — unmapped agent, absent models section, or a tier
    /// missing from the vendor map — and the caller emits <c>model: inherit</c> so the
    /// agent runs on the session model instead of silently downgrading.
    /// </summary>
    internal static string? ResolveModel(ModelsConfig? models, string agentName)
        => ResolveModel(models, agentName, ClaudeModelVendor);

    internal static string? ResolveModel(ModelsConfig? models, string agentName, string vendor)
    {
        if (models == null || !models.Agents.TryGetValue(agentName, out var tier))
            return null;
        if (!models.Tiers.TryGetValue(vendor, out var vendorTiers)
            || !vendorTiers.TryGetValue(tier, out var model))
            return null;
        return model;
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
        var model = ResolveModel(models, role.Name, OpenAiModelVendor);
        // Codex has no `skills:` preload, so naming the skill to load is the only thing that
        // carries the methodology into a spawned agent (DR 045 §10). A writer role needs the
        // workspace-write sandbox to act on that methodology at all.
        var sandbox = readOnly ? "read-only" : "workspace-write";

        return $""""
            name = "{EscapeToml(role.Name)}"
            description = "{EscapeToml(role.Description)}"
            model = "{EscapeToml(model ?? "gpt-5.6-terra")}"
            sandbox_mode = "{sandbox}"

            developer_instructions = """
            You are {Article(role.Name)} **{role.Name}**. {role.Description} {stance} Load the `${role.Name}` skill before working.{contextBlock}
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
    /// Reads the role's skill template, resolves include tags, strips the frontmatter, and
    /// de-personalizes the {{AGENT_NAME}} prose — leaving the whole methodology. Every authored
    /// section survives, ## Must-Reads included: dropping it compiled coordinating skills without
    /// their context pointers and silently voided {{include:extra-must-reads}} (DR 045 §10).
    /// </summary>
    internal static string ExtractMethodology(RoleDefinition role, string projectRoot)
    {
        var raw = TemplateGenerator.ReadBuiltInTemplate(role.TemplateFile);
        // Resolve includes against the project root so project-local template-additions
        // overrides are honored regardless of the CWD dydo was invoked from.
        var resolved = TemplateGenerator.ResolveIncludes(raw, projectRoot);

        var body = StripFrontmatter(resolved);
        body = Depersonalize(body, role.Name);
        body = RenumberOrderedLists(body);

        // A trailing horizontal rule separates nothing; drop it so the body ends on content.
        body = Regex.Replace(body, @"(\s*\n---\s*)+\s*$", "\n");
        return body.Trim() + "\n";
    }

    /// <summary>
    /// Compiles the skill body for one host: the methodology with every link rewritten to
    /// resolve from the emitted skill folder.
    /// </summary>
    private static string CompileSkillBody(RoleDefinition role, string projectRoot, string skillRoot) =>
        RewriteSkillLinks(ExtractMethodology(role, projectRoot), role.Name, skillRoot);

    /// <summary>
    /// Rewrites the compiled skill body's links (DR 045 §10). Both hosts emit SKILL.md three
    /// levels below the project root, so a dydo document is <c>../../../dydo/&lt;x&gt;</c> on
    /// either — the authored climb out of Templates/ lands one folder short of that. A
    /// <c>resources/&lt;n&gt;.md</c> link becomes the host's emitted path instead: a preloaded
    /// agent reads its skill from context, with no folder to resolve a relative link against.
    /// Targets that are neither (URLs, anchors, prose in parentheses) are left alone, and every
    /// rewrite is a fixed point so a second sync is byte-identical.
    /// </summary>
    internal static string RewriteSkillLinks(string body, string roleName, string skillRoot) =>
        LinkTargetRegex().Replace(body, match =>
            $"]({RewriteLinkTarget(match.Groups[1].Value, roleName, skillRoot)})");

    private static string RewriteLinkTarget(string target, string roleName, string skillRoot)
    {
        if (target.StartsWith("resources/", StringComparison.Ordinal))
            return $"{skillRoot}/{roleName}/{target}";

        var climb = ClimbPrefixRegex().Match(target);
        var document = target[climb.Length..];
        if (!climb.Success && !document.StartsWith("dydo/", StringComparison.Ordinal))
            return target;

        return document.StartsWith("dydo/", StringComparison.Ordinal)
            ? $"../../../{document}"
            : $"../../../dydo/{document}";
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

    // Delegate to the shared frontmatter helper so the opener tolerance and empty-block handling match every
    // other reader — no strict-regex divergence (finding 8).
    private static string StripFrontmatter(string content) => FrontmatterParser.StripFrontmatter(content);

    private static string Depersonalize(string content, string roleName)
    {
        content = content.Replace($"{{{{AGENT_NAME}}}} — ", "");
        foreach (var article in new[] { "a", "an" })
            content = content.Replace($"You are **{{{{AGENT_NAME}}}}**, working as {article} **{roleName}**.",
                $"You are working as {article} **{roleName}**.");
        content = content.Replace("{{AGENT_NAME}}", "you");
        return content;
    }

    [GeneratedRegex(@"^(\s*)(\d+)\. (.*)$")]
    private static partial Regex OrderedItemRegex();

    [GeneratedRegex(@"^## Must-Reads\b.*?(?=^## |\z)", RegexOptions.Singleline | RegexOptions.Multiline)]
    private static partial Regex MustReadsSectionRegex();

    // Only a whitespace-free target is a path; "[title](Linear URL)" is prose, not a link.
    [GeneratedRegex(@"\]\(([^)\s]+)\)")]
    private static partial Regex LinkTargetRegex();

    [GeneratedRegex(@"^(?:\.\./)+")]
    private static partial Regex ClimbPrefixRegex();
}
