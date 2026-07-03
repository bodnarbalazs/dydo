namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Sync.Notion;

/// <summary>Strong-passphrase enforcement (Decision 027 §3): accept a reasonable multi-class passphrase,
/// reject the trivially weak inputs the decision calls out — too short, and single-character-class keys
/// like the "a"-repeated threat — without demanding symbol soup.</summary>
public class NotionPassphrasePolicyTests
{
    [Theory]
    [InlineData("Corr3ct-Horse-Battery")]  // upper + lower + digit + symbol
    [InlineData("blue river 4 winter")]     // lower + digit + space
    [InlineData("Password12345")]           // upper + lower + digit, exactly long enough
    public void Validate_StrongPassphrase_Accepts(string passphrase)
    {
        Assert.Null(NotionPassphrasePolicy.Validate(passphrase));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("Ab1!")]
    [InlineData("elevenchars")]  // 11 chars, one short of the minimum
    public void Validate_TooShort_Rejects(string? passphrase)
    {
        var reason = NotionPassphrasePolicy.Validate(passphrase);
        Assert.NotNull(reason);
        Assert.Contains("at least", reason!);
    }

    [Theory]
    [InlineData("aaaaaaaaaaaaaaaa")]          // the "a"-as-a-key threat
    [InlineData("abcdefghijklmnop")]          // long, but a single character class
    [InlineData("1234567890123456")]          // digits only
    public void Validate_SingleCharacterClass_Rejects(string passphrase)
    {
        var reason = NotionPassphrasePolicy.Validate(passphrase);
        Assert.NotNull(reason);
        Assert.Contains("at least two", reason!);
    }
}
