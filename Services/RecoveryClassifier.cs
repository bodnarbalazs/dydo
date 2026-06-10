namespace DynaDocs.Services;

using DynaDocs.Models;
using DynaDocs.Utils;

/// <summary>
/// Resume detection for the guard-side ClaimedPid auto-refresh (#0207 part 2): decides
/// whether a resumed session needs its dead ClaimedPid rewritten, and emits the watchdog-log
/// <c>resume_outcome</c> for the same-session-reclaim path. Kept off <see cref="AgentRegistry"/>
/// so the high-traffic guard path's cyclomatic complexity stays under the gap_check threshold.
/// </summary>
internal static class RecoveryClassifier
{
    /// <summary>
    /// Decision returned by <see cref="ShouldRefreshResumedPid"/>. <c>ShouldRefresh=false</c>
    /// means one of the trigger clauses is unmet and the caller must return without touching
    /// .session. When true, <c>AgentName</c> and <c>LivePid</c> carry the resolved values the
    /// under-lock refresh needs — the pre-lock session snapshot is intentionally not carried
    /// because the under-lock half re-reads .session to defeat the race with a concurrent winner.
    /// </summary>
    internal readonly record struct ResumedPidRefreshDecision(
        bool ShouldRefresh, string? AgentName, int LivePid);

    /// <summary>
    /// #0207 part 2 trigger predicate (steps 1–5 of the plan's RefreshResumedAgentSession
    /// pseudocode). Pure: takes delegates so this helper stays decoupled from
    /// <see cref="AgentRegistry"/>'s lock/IO surface, and so the high-traffic guard path's
    /// cyclomatic complexity stays under the gap_check T1 threshold. Returns
    /// <c>ShouldRefresh=true</c> iff ALL of: sessionId non-empty; agent owns sessionId and
    /// is Working; .session matches sessionId and has a ClaimedPid; ClaimedPid is dead;
    /// a live claude ancestor exists. The under-lock half (steps 7–11) re-validates everything.
    /// </summary>
    internal static ResumedPidRefreshDecision ShouldRefreshResumedPid(
        string? sessionId,
        Func<string?, AgentState?> getCurrentAgent,
        Func<string, AgentSession?> getSession)
    {
        if (string.IsNullOrEmpty(sessionId)) return default;

        var agent = getCurrentAgent(sessionId);
        if (agent == null || agent.Status != AgentStatus.Working) return default;

        var session = getSession(agent.Name);
        if (session == null || session.SessionId != sessionId) return default;
        if (session.ClaimedPid is not int claimedPid) return default;

        // PID reuse: if the dead claim PID has been recycled to a live unrelated process,
        // we skip here. Same liveness assumption the watchdog already makes; documented in
        // the plan's B3 edge case.
        if (ProcessUtils.IsProcessRunning(claimedPid)) return default;

        // Never write a null PID — would break F11 for everyone.
        var livePid = ProcessUtils.FindClaudeAncestor();
        if (livePid is not int live) return default;

        return new ResumedPidRefreshDecision(true, agent.Name, live);
    }

    /// <summary>
    /// Same-session reclaim emission. Called by <see cref="AgentRegistry.HandleExistingSession"/>
    /// AFTER <c>ResetResumeBookkeeping</c> wipes the on-disk resume bookkeeping — but the
    /// in-memory <paramref name="priorState"/> snapshot is from before the reset, so its
    /// LastResumeLaunchedAt and ResumeAttempts are still pre-reset. Only emits when the prior
    /// episode actually had a watchdog launch (LastResumeLaunchedAt non-null); a same-session
    /// reclaim without one is the silent idempotent path. Writes a resume_outcome=succeeded
    /// line to the main watchdog log. All exceptions are swallowed: instrumentation must
    /// never block a claim.
    /// </summary>
    public static void EmitAutoRecovery(string? basePath,
        string agentName, AgentSession existingSession, AgentState? priorState)
    {
        var priorLaunchedAt = priorState?.LastResumeLaunchedAt;
        if (priorLaunchedAt == null) return;
        var priorAttempts = priorState?.ResumeAttempts ?? 0;
        try
        {
            var mainDydoRoot = PathUtils.FindMainDydoRoot(basePath);
            if (mainDydoRoot == null) return;
            var elapsed = (int)(System.DateTime.UtcNow - priorLaunchedAt.Value).TotalSeconds;
            WatchdogLogger.LogResumeOutcome(mainDydoRoot, agentName, existingSession.SessionId,
                outcome: "succeeded", attempts: priorAttempts,
                elapsedSeconds: elapsed, reason: "same_session_reclaim");
        }
        catch { /* never block a claim on logging */ }
    }
}
