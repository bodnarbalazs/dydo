namespace GateMetrics;

public sealed record AssemblyFacts(string Sha256, string PdbSha256, string ModuleId,
    IReadOnlyList<CompiledMethod> Methods);
