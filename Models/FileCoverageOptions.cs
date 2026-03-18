namespace DynaDocs.Models;

public record FileCoverageOptions(
    int SinceDays = 365,
    string? PathFilter = null,
    bool GapsOnly = false,
    bool SummaryOnly = false,
    string? OutputPath = null
);
