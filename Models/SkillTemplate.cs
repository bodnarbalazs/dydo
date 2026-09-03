namespace DynaDocs.Models;

/// <summary>
/// One shipped skill template (skill-&lt;name&gt;.template.md) as the compiler reads it — the
/// template IS the metadata: its frontmatter carries every key below, and its body carries the
/// whole methodology.
/// </summary>
public class SkillTemplate
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string TemplateFile { get; init; }

    /// <summary>
    /// True: <c>dydo sync</c> compiles BOTH a spawnable agent and a skill — a worker.
    /// False: a skill only — a hat, a method, or a human command, applied by a session in its
    /// own thread and never spawned. Frontmatter key: <c>emit: agent | skill</c>.
    /// </summary>
    public bool EmitAgent { get; init; }

    /// <summary>
    /// A read-only skill assesses and reports without modifying project files — sync
    /// compiles it to a no-Edit/Write tool profile. Frontmatter key: <c>read-only: true</c>.
    /// </summary>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// A delegating skill may spawn agents — sync grants its compiled Claude agent the Agent
    /// tool. Workers never delegate, so fan-out stays a decision the template declares.
    /// Frontmatter key: <c>delegates: true</c>.
    /// </summary>
    public bool Delegates { get; init; }

    /// <summary>
    /// Whether the skill may be selected automatically or only when the human explicitly
    /// invokes it. Frontmatter key: <c>invocation: automatic | explicit</c>. Missing metadata
    /// preserves the native default: automatic discovery.
    /// </summary>
    public bool ExplicitInvocation { get; init; }

    /// <summary>
    /// A web-enabled skill reaches outside the repository — sync grants its compiled agent
    /// Claude's <c>WebFetch</c>/<c>WebSearch</c> and flips Codex's <c>web_search</c> toggle.
    /// Frontmatter key: <c>web: true</c>.
    /// </summary>
    public bool Web { get; init; }

    /// <summary>
    /// What the human is expected to type after the skill's name, shown by the host as the
    /// prompt for that argument. Frontmatter key: <c>argument-hint: "&lt;hint&gt;"</c>; null
    /// when the source declares none, and the hosts show nothing.
    /// </summary>
    public string? ArgumentHint { get; init; }
}
