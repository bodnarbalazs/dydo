namespace DynaDocs.Sync.Notion;

/// <summary>One cheap-tick outcome, summarised for the daemon's single per-tick log line (ns-13). A quiet tick
/// (nothing changed remotely or locally) carries all-zero counters and <see cref="Quiet"/> true.</summary>
public sealed record NotionDeltaTickResult(
    int Created,
    int Updated,
    int Archived,
    int Conflicts,
    int FuseTrips,
    int Reconciled,
    bool Census)
{
    /// <summary>HTTP requests this tick issued (set by <see cref="NotionSpineDelta.Run"/> from the client's counter).
    /// Logged per tick so a steady quiet tick's cost is visible without arithmetic (ns-13).</summary>
    public int Requests { get; init; }

    public bool Quiet => Reconciled == 0 && Created == 0 && Updated == 0 && Archived == 0 && Conflicts == 0 && FuseTrips == 0;

    public static NotionDeltaTickResult Empty(bool census) => new(0, 0, 0, 0, 0, 0, census);

    public NotionDeltaTickResult Add(NotionDeltaTickResult other) => new(
        Created + other.Created,
        Updated + other.Updated,
        Archived + other.Archived,
        Conflicts + other.Conflicts,
        FuseTrips + other.FuseTrips,
        Reconciled + other.Reconciled,
        Census || other.Census);
}
