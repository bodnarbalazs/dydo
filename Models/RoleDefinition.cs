namespace DynaDocs.Models;

public class RoleDefinition
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public bool Base { get; init; }
    public required List<string> WritablePaths { get; init; }
    public required List<string> ReadOnlyPaths { get; init; }
    public required string TemplateFile { get; init; }
    public string? DenialHint { get; init; }
    public bool CanOrchestrate { get; init; }
    public List<RoleConstraint> Constraints { get; init; } = [];
    public List<ConditionalMustRead> ConditionalMustReads { get; init; } = [];
}
