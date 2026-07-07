namespace DynaDocs.Models;

/// <summary>
/// Represents an agent's session claim.
/// Identity is tracked via session_id provided by the coding tool's hook system (e.g., Claude Code).
/// </summary>
public class AgentSession
{
    public const string UnknownHost = "unknown";
    public const string UnknownModel = "unknown";

    private string? _host = UnknownHost;
    private string? _model = UnknownModel;

    public required string Agent { get; init; }
    public required string SessionId { get; init; }
    public string Host
    {
        get => NormalizeHost(_host);
        init => _host = value;
    }
    public string Model
    {
        get => NormalizeModel(_model);
        init => _model = value;
    }
    public DateTime Claimed { get; set; }

    // PID whose liveness indicates the claiming Claude tab is still around.
    // Used by the stale-working reclaim path (decision 018): if this PID is
    // dead and Status has been Working past StaleWorkingMinutes, the agent
    // becomes reservable. Nullable for backward-compat with old .session files.
    public int? ClaimedPid { get; set; }

    public static string NormalizeHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return UnknownHost;

        return host.Trim().ToLowerInvariant() switch
        {
            "claude" => "claude",
            "codex" => "codex",
            _ => UnknownHost
        };
    }

    public static string NormalizeModel(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return UnknownModel;

        return model.Trim();
    }
}
