namespace DynaDocs.Models;

public record FileCoverageEntry(
    string RelativePath,
    int RawScore,
    int AdjustedScore,
    string Status
);
