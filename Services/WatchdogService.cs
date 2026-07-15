namespace DynaDocs.Services;

/// <summary>
/// STUB — reserved for the DR-041 Notion-sync daemon (resolved-3): a ~15s background
/// sync of the PM records to Notion, self-started by the dydo CLI on guard trigger,
/// which also gives collaborators file-sync between commits. That daemon is a separate
/// follow-up task and is NOT implemented here.
///
/// The agent-lifecycle orchestration guts this class used to hold — auto-close terminal
/// killing, crash auto-resume + terminal relaunch, anchor/host-PID liveness sweeps,
/// orphaned-wait cleanup, the needs-human sweep, and model-cap restoration — were
/// stripped in the 2.1.0 simplification campaign (DR-041). <see cref="Commands.WatchdogCommand"/>
/// still binds start/stop/run to this class as a harmless stub until the Notion daemon
/// is built.
/// </summary>
public static class WatchdogService
{
    /// <summary>
    /// Retained transitionally, not used by the stub: <c>AgentRegistry.ResumeInFlight</c>
    /// (resume bookkeeping, itself a carve target) still reads this warmup gate. It moves
    /// out with that bookkeeping when AgentRegistry is carved.
    /// </summary>
    internal static readonly TimeSpan ResumeWarmupGate = TimeSpan.FromMinutes(5);

    /// <summary>Test hook paired with <see cref="ResumeWarmupGate"/> — see the note there.</summary>
    internal static TimeSpan? ResumeWarmupGateOverride { get; set; }
}
