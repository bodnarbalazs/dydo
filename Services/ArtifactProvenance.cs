namespace DynaDocs.Services;

/// <summary>
/// Runtime provenance stamped onto artifacts (issues, messages, reviews, task records). c1-6
/// resolves the display name AT THIS SOURCE — <see cref="Model"/> already carries the human
/// display name (<c>Opus 4.8</c>) rather than a raw id — so every consumer that flows through
/// <see cref="FromSession"/> gets it with zero per-consumer edits and no risk of per-surface
/// drift. Rendering rule (shared with whoami/agent-list via <see cref="ModelDisplay"/>):
/// the display model when known, the vendor only as a fallback when the model is unknown.
/// </summary>
internal sealed class ArtifactProvenance
{
    public required string Agent { get; init; }
    public required string Vendor { get; init; }
    public required string Model { get; init; }

    public static ArtifactProvenance? FromSession(AgentRegistry registry, string agentName)
    {
        var session = registry.GetSession(agentName);
        if (session == null) return null;

        var models = new ConfigService().LoadConfig()?.Models;

        return new ArtifactProvenance
        {
            Agent = agentName,
            Vendor = session.Host,
            Model = ModelDisplay.ResolveOrVendor(session.Model, session.Host, models)
        };
    }
}
