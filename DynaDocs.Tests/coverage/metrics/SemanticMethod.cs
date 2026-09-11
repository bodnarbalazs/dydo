namespace GateMetrics;

public sealed record SemanticMethod(string Key, string Path, int Line, int Column, int EndLine, int EndColumn);
