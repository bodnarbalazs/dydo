namespace GateMetrics;

public sealed record ConstructorFacts(string Key, int Cognitive, int Parameters, IReadOnlyList<string> Fragments);
