namespace DynaDocs.Services.Map.Contract;

/// <summary>
/// A team's Project as the picker needs it. <c>TargetDate</c> is Linear's timeless date
/// (<c>yyyy-MM-dd</c>); <c>CompletedAt</c> and <c>CanceledAt</c> are ISO timestamps. Each is null when unset.
/// </summary>
internal sealed record MapProject(
    string Id,
    string Name,
    string Url,
    MapProjectStatus Status,
    string? TargetDate,
    string? CompletedAt,
    string? CanceledAt);
