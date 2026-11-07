namespace DynaDocs.Services;

/// <summary>A push's branch changes, equal when their items are, so routes keep their value equality.</summary>
internal sealed record BranchChanges(IReadOnlyList<BranchChange> Items)
{
    public static readonly BranchChanges None = new([]);

    public bool Equals(BranchChanges? other) => other != null && Items.SequenceEqual(other.Items);

    public override int GetHashCode() => Items.Aggregate(Items.Count, (hash, change) => HashCode.Combine(hash, change));
}
