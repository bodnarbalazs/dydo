namespace DynaDocs.Models;

/// <summary>
/// A role as discovered from its mode template (mode-&lt;name&gt;.template.md) — the template
/// IS the role; its frontmatter carries the metadata. There is no separate role file layer
/// (the *.role.json disk layer was removed with the DR-041 residue hunt).
/// </summary>
public class RoleDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string TemplateFile { get; init; }

    /// <summary>
    /// True: <c>dydo sync</c> emits BOTH a native sub-agent and a skill (worker roles,
    /// spawned as typed sub-agents). False: skill only — the role is a methodology a
    /// Tier-1 identity applies in its own thread, never a spawnable sub-agent.
    /// Frontmatter key: <c>emit: agent | skill</c>.
    /// </summary>
    public bool EmitAgent { get; init; }

    /// <summary>
    /// A read-only role assesses and reports without modifying project files — sync
    /// compiles it to a no-Edit/Write tool profile. Frontmatter key: <c>read-only: true</c>.
    /// </summary>
    public bool ReadOnly { get; init; }
}
