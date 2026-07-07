namespace DynaDocs.Services;

internal sealed class ArtifactProvenance
{
    public required string Agent { get; init; }
    public required string Vendor { get; init; }
    public required string Model { get; init; }

    public static ArtifactProvenance? FromSession(AgentRegistry registry, string agentName)
    {
        var session = registry.GetSession(agentName);
        if (session == null) return null;

        return new ArtifactProvenance
        {
            Agent = agentName,
            Vendor = session.Host,
            Model = session.Model
        };
    }
}
