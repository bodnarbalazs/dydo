namespace DynaDocs.Models;

public class AgentState
{
    public required string Name { get; init; }
    public string? Role { get; set; }
    public string? Task { get; set; }
    public AgentStatus Status { get; set; } = AgentStatus.Free;
    public DateTime? Since { get; set; }
    public List<string> WritablePaths { get; set; } = [];
    public List<string> ReadOnlyPaths { get; set; } = [];
    public string? AssignedHuman { get; set; }

    /// <summary>
    /// Tracks which roles this agent has held on which tasks.
    /// Key: task name, Value: list of roles held (e.g., ["planner", "code-writer"])
    /// Used to prevent self-review (code-writer cannot become reviewer on same task).
    /// </summary>
    public Dictionary<string, List<string>> TaskRoleHistory { get; set; } = new();

    /// <summary>
    /// Project-relative paths of must-read files that haven't been read yet.
    /// Populated on role set, cleared as agent reads each file.
    /// </summary>
    public List<string> UnreadMustReads { get; set; } = [];

    /// <summary>
    /// "A human is needed" attention flag (Decision 030 §1). Usually machine-written and derived from
    /// observable events (an AskUserQuestion tool call, a turn that ends while working on an in-flight
    /// task, or a crashed session), in which case it self-heals: the agent's next
    /// guarded tool call clears it, as does the watchdog's reconcile sweep once the cause disappears.
    /// An operator can instead raise it deliberately via <c>dydo hand raise</c> — see
    /// <see cref="NeedsHumanSource"/> for the derived-vs-explicit distinction that governs clearing.
    /// Mirrored to the current task file's frontmatter.
    /// </summary>
    public bool NeedsHuman { get; set; }

    /// <summary>
    /// Provenance of <see cref="NeedsHuman"/> when it is set (Decision 030 §1): a machine-detected
    /// <see cref="Models.NeedsHumanSource.Derived"/> flag self-heals, an operator-raised
    /// <see cref="Models.NeedsHumanSource.Explicit"/> flag is sticky. Null whenever
    /// <see cref="NeedsHuman"/> is false — clearing the flag drops the source.
    /// </summary>
    public NeedsHumanSource? NeedsHumanSource { get; set; }

    /// <summary>
    /// Name of the agent that dispatched this agent. Null for human-initiated agents (tree roots).
    /// </summary>
    public string? DispatchedBy { get; set; }

    /// <summary>
    /// Role of the agent that dispatched this agent. Used by dispatch-restriction constraints.
    /// </summary>
    public string? DispatchedByRole { get; set; }

    /// <summary>
    /// GUID-based window identifier for Windows Terminal routing.
    /// Survives release so the watchdog can correlate processes to windows.
    /// </summary>
    public string? WindowId { get; set; }

    /// <summary>
    /// Whether the watchdog should auto-close this agent's terminal after release.
    /// Survives release so the watchdog can act on it.
    /// </summary>
    public bool AutoClose { get; set; }
}
