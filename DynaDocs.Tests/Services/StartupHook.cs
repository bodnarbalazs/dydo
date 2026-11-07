using System.Globalization;

// DOTNET_STARTUP_HOOKS runs this before Main of a child dydo process (DangerousPatternCultureTests).
// The runtime finds the hook by this type name, outside any namespace. It sets the process culture from
// DYDO_TEST_STARTUP_CULTURE and fails the start rather than go on in a culture without its casing data.
internal static class StartupHook
{
    public const string CultureVariable = "DYDO_TEST_STARTUP_CULTURE";
    public const string CultureMarker = "dydo test startup culture: ";

    public static void Initialize()
    {
        var name = Environment.GetEnvironmentVariable(CultureVariable)
            ?? throw new InvalidOperationException("startup hook loaded without a culture to set");

        // Without ICU (invariant globalization) this throws CultureNotFoundException.
        var culture = CultureInfo.GetCultureInfo(name);
        if (culture.TextInfo.ToUpper('i') != 'İ')
            throw new InvalidOperationException($"{name} does not upper-case i to dotted I: its ICU casing data is missing");

        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.CurrentCulture = culture;
        Console.Error.WriteLine(CultureMarker + CultureInfo.CurrentCulture.Name);
    }
}
