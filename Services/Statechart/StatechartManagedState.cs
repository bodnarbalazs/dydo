namespace DynaDocs.Services.Statechart;

// Verified paths, plus the components to re-stamp after an update and those that failed verification.
internal sealed record StatechartManagedState(StatechartDependencies Dependencies, string[] Stale, string[] Damaged);
