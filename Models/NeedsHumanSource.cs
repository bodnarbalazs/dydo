namespace DynaDocs.Models;

/// <summary>
/// Provenance of an agent's <see cref="AgentState.NeedsHuman"/> flag (Decision 030 §1). A
/// <see cref="Derived"/> flag is machine-detected (an AskUserQuestion tool call, a Stop-hook
/// turn-end while mid-task, or the watchdog's crash sweep) and self-heals: the agent's next guarded
/// tool call clears it and the watchdog reconcile sweep clears it once its cause disappears. An
/// <see cref="Explicit"/> flag was raised deliberately via <c>dydo hand raise</c> and is sticky: it
/// is cleared only by <c>dydo hand lower</c>, by agent release, or by a human — never by the next
/// tool call and never by the sweep. A derived flag upgraded by an explicit raise becomes explicit;
/// a derived detection never downgrades an already-explicit flag.
/// </summary>
public enum NeedsHumanSource
{
    Derived,
    Explicit
}
