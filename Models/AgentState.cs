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
    public List<string> UnreadMessages { get; set; } = [];

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

    /// <summary>
    /// Number of times the watchdog has auto-resumed this agent's claude session
    /// since the last claim/release. Reset to 0 on claim and release. Capped per
    /// Decision 022. (Resume bookkeeping is an AgentRegistry carve target — DR-041.)
    /// </summary>
    public int ResumeAttempts { get; set; }

    /// <summary>
    /// UTC timestamp of the most recent watchdog auto-resume launch. Used to
    /// suppress duplicate launches during the resumed claude's warmup window
    /// (Decision 022, #0152). Cleared on dydo agent claim and release.
    /// </summary>
    public DateTime? LastResumeLaunchedAt { get; set; }

    /// <summary>
    /// The .session.ClaimedPid value observed at the moment of the most recent
    /// auto-resume launch. Used to detect bad-session-ID failures: if the
    /// ClaimedPid still matches this after the warmup gate elapses, the resumed
    /// claude never reached its claim hook and we fail fast. Cleared on claim/release.
    /// </summary>
    public int? PreResumePid { get; set; }

    /// <summary>
    /// PID of the resume terminal process the watchdog launched at the most recent
    /// auto-resume. Used by the watchdog's bad-session fail-fast check
    /// to distinguish "still rehydrating" (launched claude is alive but slow to refresh
    /// its ClaimedPid) from "genuinely failed" (launched claude is dead). The wall-clock
    /// warmup gate alone produces false positives on long rehydrations — the launched-PID
    /// liveness check is what makes the watchdog log honest. Cleared on claim/release.
    /// Closes #0173.
    /// </summary>
    public int? LaunchedPid { get; set; }
}
