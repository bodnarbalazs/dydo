namespace DynaDocs.Tests.Sync.Notion;

/// <summary>
/// Serializes the vault test classes that run Argon2id derivations (Decision 027 §5). Assembly-wide
/// parallelization is already disabled (see AssemblyInfo.cs), so this is defense-in-depth: it pins the
/// memory-hard derivations to run one at a time even if that assembly setting were ever relaxed, so their
/// working sets can never stack. The Console-capturing connect/reveal vault tests are locked to the
/// "ConsoleOutput" collection instead; the same assembly-wide disable keeps them from stacking too.
/// </summary>
[CollectionDefinition("NotionArgon2id", DisableParallelization = true)]
public class NotionArgon2idCollection
{
}
