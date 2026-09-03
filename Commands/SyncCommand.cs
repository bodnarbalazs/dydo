namespace DynaDocs.Commands;

using System.CommandLine;
using System.Text.RegularExpressions;
using DynaDocs.Models;
using DynaDocs.Services;
using DynaDocs.Utils;

/// <summary>
/// Compiles skill templates into native artifacts (Decision 024):
/// Claude Code <c>.claude/agents/&lt;name&gt;.md</c> / <c>.claude/skills/&lt;name&gt;/SKILL.md</c>
/// and Codex <c>.codex/agents/&lt;name&gt;.toml</c> / <c>.agents/skills/&lt;name&gt;/SKILL.md</c>.
///
/// The template IS the source: its frontmatter supplies the metadata (description, emit shape,
/// invocation policy, delegation, read-only → tool profile) and its body supplies the whole
/// methodology — context pointers included.
///
/// Two emission shapes (Decision 024 native pivot):
/// - <c>emit: agent</c> compiles BOTH an agent and a skill — a worker, spawned as a typed
///   agent whose definition is a thin identity wrapper that preloads its skill (DR 045 §10).
/// - <c>emit: skill</c> compiles a skill and NO agent: a hat, a method, or a human command a
///   session applies in its own thread, never spawned.
/// </summary>
public static partial class SyncCommand
{
    // Skills dydo no longer ships. A retired name leaves the shipped template set
    // (TemplateGenerator.GetBuiltInSkillTemplateNames), so it is never discovered, and the
    // sweep below removes whatever it last compiled. This is deliberately not a generic
    // output-directory cleaner. DR 046 retired `code-writer` and `issue-planner` by renaming
    // them to `implementer` and `specifier`; `implementer` is a shipped skill again, so listing
    // it here would make every sync sweep its own output.
    internal static readonly string[] RetiredSkills = ["sprint-auditor", "orchestrator", "manager", "planner", "test-writer", "code-writer", "issue-planner"];

    // Workflow harnesses dydo no longer ships (DR 045: the run-sprint loop became the
    // Issue Captain's completion criterion). Claude is the only host with a workflow surface.
    private static readonly string[] RetiredWorkflows = ["run-sprint.js"];

    // Skill resources retired by rename (DR 045: merge-sprint became merge; the broad plan
    // rubric split into project-plan and issue-plan; the generic planner split into two skills;
    // DR 046: issue-plan became spec) or by promotion (the research scout became the `scout`
    // agent), swept from both hosts' skill folders.
    private static readonly string[] RetiredSkillResources =
    [
        "reviewer/resources/merge-sprint.md",
        "reviewer/resources/plan.md",
        "reviewer/resources/issue-plan.md",
        "planner/resources/project.md",
        "planner/resources/issue.md",
        "research/resources/scout.md"
    ];

    // Where each host emits a skill: <root>/<name>/SKILL.md, three levels below the project
    // root on both, which is why one link rewrite serves them equally.
    private const string ClaudeSkillRoot = ".claude/skills";
    private const string CodexSkillRoot = ".agents/skills";

    // Vendor key used when compiling Claude-native artifacts (Decision 028 §2). A future
    // Codex target reads a different vendor key from the same tiers map; the agent → tier
    // section never changes per vendor.
    private const string ClaudeModelVendor = "anthropic";
    private const string OpenAiModelVendor = "openai";

    public static Command Create()
    {
        var command = new Command("sync", "Compile skill templates into native agents and skills");
        command.SetAction(_ => Execute());
        return command;
    }

    internal static int Execute(string? projectRoot = null)
    {
        projectRoot ??= PathUtils.FindProjectRoot() ?? Environment.CurrentDirectory;
        var templates = SkillTemplateService.DiscoverSkills();
        CleanRetiredArtifacts(projectRoot);
        var config = new ConfigService().LoadConfig(projectRoot);
        var models = config?.Models;
        var (emitClaude, emitCodex) = ResolveIntegrationTargets(config?.Integrations);
        var (agents, skills) =
            SyncDiscoveredSkills(templates, projectRoot, models, emitClaude, emitCodex);

        if (emitCodex)
            WriteCodexHooks(projectRoot);

        var workflows = emitClaude ? SyncWorkflows(projectRoot) : 0;
        PrintSyncSummary(agents, skills, workflows, emitClaude, emitCodex);
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

    private static (List<SkillTemplate> Agents, List<SkillTemplate> Skills)
        SyncDiscoveredSkills(
            IReadOnlyCollection<SkillTemplate> templates,
            string projectRoot,
            ModelsConfig? models,
            bool emitClaude,
            bool emitCodex)
    {
        var agents = templates.Where(skill => skill.EmitAgent).ToList();
        foreach (var skill in agents)
        {
            if (emitClaude) SyncAgent(skill, projectRoot, models);
            if (emitCodex) SyncCodexAgent(skill, projectRoot, models);
        }

        var skills = templates.Where(skill => !skill.EmitAgent).ToList();
        foreach (var skill in skills)
        {
            if (emitClaude) SyncSkill(skill, projectRoot);
            if (emitCodex) SyncCodexSkill(skill, projectRoot);
        }

        return (agents, skills);
    }

    private static void PrintSyncSummary(
        IReadOnlyCollection<SkillTemplate> agents,
        IReadOnlyCollection<SkillTemplate> skills,
        int workflows,
        bool emitClaude,
        bool emitCodex)
    {
        if (emitClaude)
        {
            Console.WriteLine($"Synced {agents.Count} agent(s) to .claude/ (agents + skills): {string.Join(", ", agents.Select(s => s.Name))}");
            Console.WriteLine($"Synced {skills.Count} skill(s) to .claude/ (skills only): {string.Join(", ", skills.Select(s => s.Name))}");
            Console.WriteLine($"Synced {workflows} workflow(s) to .claude/workflows.");
        }
        if (emitCodex)
            Console.WriteLine($"Synced Codex artifacts to .agents/skills and .codex/agents.");
        if (!emitClaude || !emitCodex)
            Console.WriteLine($"Skipped {(emitClaude ? "Codex" : "Claude")} artifacts — not recorded in dydo.json integrations (add it with 'dydo init <integration> --join').");
    }

    /// <summary>
    /// Removes compiler-owned files dydo no longer emits: allowlisted retired skills, retired
    /// workflow harnesses, and resources retired by rename. Folders are removed only when
    /// empty so project-owned siblings survive.
    /// </summary>
    internal static int CleanRetiredArtifacts(string projectRoot)
    {
        var removed = 0;

        foreach (var skillName in RetiredSkills)
        {
            var skillRemoved = Sweep(
                Path.Combine(projectRoot, ".claude", "agents", $"{skillName}.md"),
                Combine(projectRoot, ClaudeSkillRoot, $"{skillName}/agents/openai.yaml"),
                Combine(projectRoot, ClaudeSkillRoot, $"{skillName}/SKILL.md"),
                Path.Combine(projectRoot, ".codex", "agents", $"{skillName}.toml"),
                Combine(projectRoot, CodexSkillRoot, $"{skillName}/agents/openai.yaml"),
                Combine(projectRoot, CodexSkillRoot, $"{skillName}/SKILL.md"));

            // A skill retired after its SKILL.md was already swept keeps its folder alive through
            // agents/openai.yaml alone, so the folder outlives the file DeleteIfPresent emptied.
            DeleteIfEmpty(Combine(projectRoot, ClaudeSkillRoot, skillName));
            DeleteIfEmpty(Combine(projectRoot, CodexSkillRoot, skillName));

            if (skillRemoved > 0)
                Console.WriteLine($"Removed retired skill artifacts for '{skillName}'.");
            removed += skillRemoved;
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

    /// <summary>Removes a retired skill's own folder once the sweep left it empty.</summary>
    private static void DeleteIfEmpty(string folder)
    {
        if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
            Directory.Delete(folder);
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

    internal static void SyncAgent(SkillTemplate skill, string projectRoot, ModelsConfig? models = null)
    {
        var agentDir = Path.Combine(projectRoot, ".claude", "agents");
        Directory.CreateDirectory(agentDir);
        WriteLf(Path.Combine(agentDir, $"{skill.Name}.md"), BuildAgent(skill, ExtractMustReads(skill, projectRoot), models));

        WriteSkill(skill, projectRoot);
    }

    /// <summary>
    /// Emits the skill and no agent (Decision 024): a hat, a method, or a human command a
    /// session applies in its own thread, never spawned.
    /// </summary>
    internal static void SyncSkill(SkillTemplate skill, string projectRoot) =>
        WriteSkill(skill, projectRoot);

    internal static void SyncCodexAgent(SkillTemplate skill, string projectRoot, ModelsConfig? models = null)
    {
        SyncCodexSkill(skill, projectRoot);

        var agentDir = Path.Combine(projectRoot, ".codex", "agents");
        Directory.CreateDirectory(agentDir);
        WriteLf(Path.Combine(agentDir, $"{skill.Name}.toml"),
            BuildCodexAgent(skill, ExtractMustReads(skill, projectRoot), models));
    }

    internal static void SyncCodexSkill(SkillTemplate skill, string projectRoot)
    {
        var skillDir = Path.Combine(projectRoot, ".agents", "skills", skill.Name);
        Directory.CreateDirectory(skillDir);
        WriteLf(
            Path.Combine(skillDir, "SKILL.md"),
            BuildSkill(skill, CompileSkillBody(skill, projectRoot, CodexSkillRoot), emitClaudePolicy: false));
        WriteCodexSkillMetadata(skill, skillDir);
        WriteSkillResources(skill, skillDir);
    }

    internal static void WriteCodexHooks(string projectRoot)
        => InitCommand.ConfigureCodexHooks(projectRoot);

    private static void WriteSkill(SkillTemplate skill, string projectRoot)
    {
        var skillDir = Path.Combine(projectRoot, ".claude", "skills", skill.Name);
        Directory.CreateDirectory(skillDir);
        WriteLf(
            Path.Combine(skillDir, "SKILL.md"),
            BuildSkill(skill, CompileSkillBody(skill, projectRoot, ClaudeSkillRoot), emitClaudePolicy: true));
        WriteSkillResources(skill, skillDir);
    }

    /// <summary>
    /// Codex's expression of the two facts Claude carries in SKILL.md frontmatter: the
    /// explicit-invocation policy and the argument hint. Written when the skill declares either,
    /// and removed with the folder it empties when it declares neither — a skill that loses a
    /// declaration must not keep enforcing it from a file no later sync would touch.
    /// </summary>
    private static void WriteCodexSkillMetadata(SkillTemplate skill, string skillDir)
    {
        var agentsDir = Path.Combine(skillDir, "agents");
        var metadataFile = Path.Combine(agentsDir, "openai.yaml");

        var metadata =
            (skill.ExplicitInvocation ? "policy:\n  allow_implicit_invocation: false\n" : "")
            + (skill.ArgumentHint == null
                ? ""
                : $"interface:\n  default_prompt: \"{EscapeQuoted(skill.ArgumentHint)}\"\n");

        if (metadata.Length > 0)
        {
            Directory.CreateDirectory(agentsDir);
            WriteLf(metadataFile, metadata);
            return;
        }

        if (!File.Exists(metadataFile))
            return;

        File.Delete(metadataFile);
        if (!Directory.EnumerateFileSystemEntries(agentsDir).Any())
            Directory.Delete(agentsDir);
    }

    /// <summary>
    /// Skill resource templates (<skill>-resource-<name>.template.md) compile into the
    /// skill folder's resources/ (DR-039 review-target subskills; DR-042). Resource bodies are
    /// copied verbatim: they are authored one folder deeper than SKILL.md and already carry the
    /// climbs that resolve from resources/, so the skill-body link rewrite must not reach them.
    /// </summary>
    private static void WriteSkillResources(SkillTemplate skill, string skillDir)
    {
        foreach (var (fileName, content) in TemplateGenerator.GetSkillResources(skill.Name))
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
    /// The native agent definition: a thin identity wrapper over the skill plus the tool profile
    /// derived from its permission shape. A skill that can write nothing is read-only for the
    /// codebase, so its agent gets no Edit/Write — that is how "reviewers don't write code"
    /// becomes natively enforced rather than guard-RBAC enforced. Every agent carries
    /// <c>skills:</c>, which preloads the skill's full content at startup, and the Skill tool, so
    /// the methodology actually reaches the spawned agent; the Agent tool is granted only when the
    /// frontmatter declares <c>delegates: true</c>, so workers still cannot fan out (DR 045 §10).
    /// </summary>
    private static string BuildAgent(SkillTemplate skill, List<string> mustReads, ModelsConfig? models = null)
    {
        var readOnly = skill.ReadOnly;
        var tools = readOnly
            ? "Read, Grep, Glob, Bash, Skill"
            : "Read, Grep, Glob, Bash, Edit, Write, Skill";
        if (skill.Delegates)
            tools += ", Agent";
        if (skill.Web)
            tools += ", WebFetch, WebSearch";
        var stance = readOnly
            ? "You are read-only: you assess and report, you do not modify the project's files."
            : "You produce and modify the project's files as your task requires.";
        var contextBlock = mustReads.Count == 0 ? "" :
            "\n\nRead these for project context before working:\n"
            + string.Join('\n', mustReads.Select(p => $"- {p}")) + "\n";

        // Decision 028: agent → tier → concrete model, bound here by the compiler so
        // workflows stay tier-blind. An unresolved agent emits `model: inherit` — the
        // explicit no-silent-downgrade spelling (an OMITTED model would fall back to
        // Claude Code's default subagent model, not the session model).
        var model = ResolveModel(models, skill.Name);

        return $"""
            ---
            name: {skill.Name}
            description: {skill.Description}
            tools: {tools}
            skills: [{skill.Name}]
            model: {model ?? "inherit"}
            ---

            You are {Article(skill.Name)} **{skill.Name}**. {skill.Description} {stance} Your methodology lives in
            the `{skill.Name}` skill; follow it.
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

    private static string BuildCodexAgent(SkillTemplate skill, List<string> mustReads, ModelsConfig? models)
    {
        // No Claude-style tool list: codex's agent `tools` is a ToolsToml struct of codex-defined
        // toggles (view_image, web_search) — NOT file/shell tool names. Claude's tool names
        // are category-wrong here and have no valid codex representation; codex grants
        // apply_patch/shell/read intrinsically and inherits toggles from the parent when the
        // struct is absent. Read-only capability is a separate concern (issue 0272,
        // sandbox_mode). See issue 0271.
        var readOnly = skill.ReadOnly;
        var stance = readOnly
            ? "You are read-only: assess and report without modifying project files."
            : "You produce and modify the project's files as your task requires.";
        var contextBlock = mustReads.Count == 0 ? "" :
            "\n\nRead these for project context before working:\n"
            + string.Join('\n', mustReads.Select(p => $"- {p}"));
        var model = ResolveModel(models, skill.Name, OpenAiModelVendor);
        // Codex has no `skills:` preload, so naming the skill to load is the only thing that
        // carries the methodology into a spawned agent (DR 045 §10). A writing agent needs the
        // workspace-write sandbox to act on that methodology at all.
        var sandbox = readOnly ? "read-only" : "workspace-write";
        // `web: true` sets the one toggle codex owns for it. A TOML table header ends the
        // top-level key section, so [tools] goes last: any key emitted after it would parse as a
        // member of the struct instead of a field of the agent.
        var webTools = skill.Web ? "\n\n[tools]\nweb_search = true" : "";

        return $""""
            name = "{EscapeQuoted(skill.Name)}"
            description = "{EscapeQuoted(skill.Description)}"
            model = "{EscapeQuoted(model ?? "gpt-5.6-terra")}"
            sandbox_mode = "{sandbox}"

            developer_instructions = """
            You are {Article(skill.Name)} **{skill.Name}**. {skill.Description} {stance} Load the `${skill.Name}` skill before working.{contextBlock}
            """{webTools}
            """";
    }

    /// <summary>
    /// Escapes an authored value for every quoted scalar it compiles into — the Codex agent's
    /// TOML basic strings, Codex's openai.yaml, and Claude's SKILL.md frontmatter all take the
    /// same backslash escape. An unescaped quote or trailing backslash ends the scalar early, and
    /// the host reads a malformed file rather than reporting one.
    /// </summary>
    private static string EscapeQuoted(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string BuildSkill(
        SkillTemplate skill,
        string methodology,
        bool emitClaudePolicy)
    {
        // Both keys are Claude SKILL.md frontmatter. Codex carries the same two facts in
        // agents/openai.yaml, so its SKILL.md must not repeat them in keys it does not read.
        var invocationPolicy = emitClaudePolicy && skill.ExplicitInvocation
            ? "\ndisable-model-invocation: true"
            : "";
        var argumentHint = emitClaudePolicy && skill.ArgumentHint != null
            ? $"\nargument-hint: \"{EscapeQuoted(skill.ArgumentHint)}\""
            : "";

        return $"""
        ---
        name: {skill.Name}
        description: {skill.Description}{argumentHint}{invocationPolicy}
        ---

        {methodology}
        """;
    }

    private static string Article(string noun) =>
        "aeiou".Contains(char.ToLowerInvariant(noun[0])) ? "an" : "a";

    /// <summary>
    /// Reads the skill template, resolves include tags, strips the frontmatter, and
    /// de-personalizes the {{AGENT_NAME}} prose — leaving the whole methodology. Every authored
    /// section survives, ## Must-Reads included: dropping it compiled coordinating skills without
    /// their context pointers and silently voided {{include:extra-must-reads}} (DR 045 §10).
    /// </summary>
    internal static string ExtractMethodology(SkillTemplate skill, string projectRoot)
    {
        var raw = TemplateGenerator.ReadBuiltInTemplate(skill.TemplateFile);
        // Resolve includes against the project root so project-local template-additions
        // overrides are honored regardless of the CWD dydo was invoked from.
        var resolved = TemplateGenerator.ResolveIncludes(raw, projectRoot);

        var body = StripFrontmatter(resolved);
        body = Depersonalize(body, skill.Name);
        body = RenumberOrderedLists(body);

        // A trailing horizontal rule separates nothing; drop it so the body ends on content.
        body = Regex.Replace(body, @"(\s*\n---\s*)+\s*$", "\n");
        return body.Trim() + "\n";
    }

    /// <summary>
    /// Compiles the skill body for one host: the methodology with every link rewritten to
    /// resolve from the emitted skill folder.
    /// </summary>
    private static string CompileSkillBody(SkillTemplate skill, string projectRoot, string skillRoot) =>
        RewriteSkillLinks(ExtractMethodology(skill, projectRoot), skill.Name, skillRoot);

    /// <summary>
    /// Rewrites the compiled skill body's links (DR 045 §10). Both hosts emit SKILL.md three
    /// levels below the project root, so a dydo document is <c>../../../dydo/&lt;x&gt;</c> on
    /// either — the authored climb out of Templates/ lands one folder short of that. A
    /// <c>resources/&lt;n&gt;.md</c> link becomes the host's emitted path instead: a preloaded
    /// agent reads its skill from context, with no folder to resolve a relative link against.
    /// Targets that are neither (URLs, anchors, prose in parentheses) are left alone, and every
    /// rewrite is a fixed point so a second sync is byte-identical.
    /// </summary>
    internal static string RewriteSkillLinks(string body, string skillName, string skillRoot) =>
        LinkTargetRegex().Replace(body, match =>
            $"]({RewriteLinkTarget(match.Groups[1].Value, skillName, skillRoot)})");

    private static string RewriteLinkTarget(string target, string skillName, string skillRoot)
    {
        if (target.StartsWith("resources/", StringComparison.Ordinal))
            return $"{skillRoot}/{skillName}/{target}";

        var climb = ClimbPrefixRegex().Match(target);
        var document = target[climb.Length..];
        if (!climb.Success && !document.StartsWith("dydo/", StringComparison.Ordinal))
            return target;

        return document.StartsWith("dydo/", StringComparison.Ordinal)
            ? $"../../../{document}"
            : $"../../../dydo/{document}";
    }

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
    /// The skill's static must-reads, taken from the [links] in the skill template's
    /// "## Must-Reads" section (normalized to dydo-relative paths) so each skill points at
    /// its own context. Conditional must-reads are task-runtime and left to the workflow.
    /// </summary>
    internal static List<string> ExtractMustReads(SkillTemplate skill, string projectRoot)
    {
        var template = TemplateGenerator.ResolveIncludes(
            TemplateGenerator.ReadBuiltInTemplate(skill.TemplateFile), projectRoot);

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

    private static string Depersonalize(string content, string skillName)
    {
        content = content.Replace($"{{{{AGENT_NAME}}}} — ", "");
        foreach (var article in new[] { "a", "an" })
            content = content.Replace($"You are **{{{{AGENT_NAME}}}}**, working as {article} **{skillName}**.",
                $"You are working as {article} **{skillName}**.");
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
