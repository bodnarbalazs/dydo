namespace DynaDocs.Sync;

/// <summary>What the engine decided for one object this tick.</summary>
public enum ReconcileAction
{
    /// <summary>Nothing changed on either side.</summary>
    None,

    /// <summary>Repo changed; the external side must be updated.</summary>
    PushToExternal,

    /// <summary>External changed; the repo file must be updated.</summary>
    WriteToRepo,

    /// <summary>Both changed and merged cleanly; write both sides, advance base.</summary>
    Merged,

    /// <summary>Both changed with a true overlap; merged with a recorded conflict.</summary>
    Conflict,

    /// <summary>New on one side; create on the other.</summary>
    Create,

    /// <summary>Present in base, gone on one side; delete on the other.</summary>
    Delete,
}
