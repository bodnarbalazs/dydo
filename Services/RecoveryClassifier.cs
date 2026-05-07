namespace DynaDocs.Services;

using DynaDocs.Models;
using DynaDocs.Utils;

/// <summary>
/// PR3 of agent-crash-fixes: classifies a Claim event for the 4-bucket recovery analysis
/// (fresh / auto / manual) and routes the watchdog-log <c>resume_outcome</c> emission for
/// the auto path. Keeping this off <see cref="AgentRegistry"/> keeps its cyclomatic
/// complexity off the gap_check threshold — the registry is already a large file and the
/// classification rules want to live next to the audit-event semantics, not the lock and
/// I/O plumbing.
/// </summary>
internal static class RecoveryClassifier
{
    /// <summary>
    /// Categorises a fresh-setup Claim event:
    ///   - No prior session OR prior status not Working/Reviewing → ("fresh", null, null)
    ///   - Prior <c>LastResumeLaunchedAt</c> non-null              → ("auto", priorSession.SessionId, priorAttempts)
    ///   - Else (prior unreleased work without resume bookkeeping) → ("manual", priorSession.SessionId, priorAttempts)
    /// The same-session reclaim path emits its own "auto" event from
    /// <see cref="AgentRegistry.HandleExistingSession"/> and never reaches this helper.
    /// </summary>
    public static (string? RecoveryKind, string? PredecessorSession, int? AttemptsAtClaim) ClassifyFreshSetup(
        AgentSession? priorSession, AgentState? priorState)
    {
        if (priorSession == null) return ("fresh", null, null);
        var hadActivePriorWork = priorState?.Status is AgentStatus.Working or AgentStatus.Reviewing;
        if (!hadActivePriorWork) return ("fresh", null, null);

        var attempts = priorState?.ResumeAttempts ?? 0;
        var kind = priorState?.LastResumeLaunchedAt != null ? "auto" : "manual";
        return (kind, priorSession.SessionId, attempts);
    }

    /// <summary>
    /// Same-session reclaim emission. Called by <see cref="AgentRegistry.HandleExistingSession"/>
    /// AFTER <c>ResetResumeBookkeeping</c> wipes the on-disk resume bookkeeping — but the
    /// in-memory <paramref name="priorState"/> snapshot is from before the reset, so its
    /// LastResumeLaunchedAt and ResumeAttempts are still pre-reset. Only emits when the prior
    /// episode actually had a watchdog launch (LastResumeLaunchedAt non-null); a same-session
    /// reclaim without one is the silent idempotent path pre-PR3 builds had. Two emissions
    /// are paired for the inquisition's join: (1) a Claim audit event with recovery_kind=auto,
    /// (2) a resume_outcome=succeeded line in the main watchdog log. All exceptions are
    /// swallowed: instrumentation must never block a claim.
    /// </summary>
    public static void EmitAutoRecovery(string? basePath, IAuditService auditService,
        string sessionId, string agentName, string? human,
        AgentSession existingSession, AgentState? priorState)
    {
        var priorLaunchedAt = priorState?.LastResumeLaunchedAt;
        if (priorLaunchedAt == null) return;
        var priorAttempts = priorState?.ResumeAttempts ?? 0;
        try
        {
            auditService.LogEvent(sessionId, new AuditEvent
            {
                EventType = AuditEventType.Claim,
                AgentName = agentName,
                RecoveryKind = "auto",
                ResumePredecessorSession = existingSession.SessionId,
                ResumeAttemptsAtClaim = priorAttempts
            }, agentName, human);

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
