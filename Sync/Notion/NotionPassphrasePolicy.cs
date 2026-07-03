namespace DynaDocs.Sync.Notion;

/// <summary>
/// Strong-passphrase enforcement for vault connect (Decision 027 §3 — the committed ciphertext is a
/// permanent offline-brute-force target, so the passphrase carries the whole security burden). The bar is
/// deliberately "good enough, not draconian": a reasonable length plus more than one character class,
/// which rejects the trivially weak inputs the decision calls out (the <c>"a"</c>-as-a-key threat, a single
/// repeated character) without demanding symbol soup. It is not a substitute for user judgement.
/// </summary>
public static class NotionPassphrasePolicy
{
    public const int MinLength = 12;

    /// <summary>Returns a human-readable rejection reason, or <c>null</c> if the passphrase clears the bar.
    /// The passphrase itself is never included in the returned message.</summary>
    public static string? Validate(string? passphrase)
    {
        if (string.IsNullOrEmpty(passphrase) || passphrase.Length < MinLength)
            return $"passphrase too weak: use at least {MinLength} characters.";

        if (CharacterClasses(passphrase) < 2)
            return "passphrase too weak: mix at least two of lower-case, upper-case, digits, and symbols.";

        return null;
    }

    private static int CharacterClasses(string s)
    {
        bool lower = false, upper = false, digit = false, other = false;
        foreach (var c in s)
        {
            if (char.IsLower(c)) lower = true;
            else if (char.IsUpper(c)) upper = true;
            else if (char.IsDigit(c)) digit = true;
            else other = true;
        }

        return (lower ? 1 : 0) + (upper ? 1 : 0) + (digit ? 1 : 0) + (other ? 1 : 0);
    }
}
