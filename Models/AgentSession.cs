namespace DynaDocs.Models;

/// <summary>
/// Host/model normalisation for hook payloads. The agent-session DTO (the <c>.session</c> claim
/// record) was carved out with the claim ceremony (DR-041); only the vendor-neutral host/model
/// normalisation the guard still uses when reading a hook payload survives.
/// </summary>
public static class AgentSession
{
    public const string UnknownHost = "unknown";
    public const string UnknownModel = "unknown";

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
