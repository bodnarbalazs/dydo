namespace GateMetrics;

public sealed record BehaviorFacts(IReadOnlyList<BehaviorFragment> Fragments,
    IReadOnlyList<ConstructorFacts> Constructors, IReadOnlyList<SemanticMethod> DeclaredMethods,
    IReadOnlyList<StructuralMethod> StructuralMethods);
