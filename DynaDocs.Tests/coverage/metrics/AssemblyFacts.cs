namespace GateMetrics;

public sealed record AssemblyFacts(string AssemblyName, string Path, long Bytes, string Sha1,
    string Sha256, string PdbSha256, string ModuleId,
    IReadOnlyList<CompiledMethod> Methods);
