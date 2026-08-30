namespace DynaDocs.Tests.Integration;

/// <summary>
/// The shipped source under <c>Templates/</c> and this repository's installed copy under
/// <c>dydo/_system/templates/</c> are one file authored twice, and `dydo sync` reads the installed
/// copy first — so a divergence silently compiles the stale one.
///
/// The comparison set is deliberately narrow: the five skills adapted from mattpocock/skills.
/// General parity is proved by `dydo template update --diff` reporting zero pending, not here;
/// widening this test would turn every legitimate source edit into a red suite until the mirror
/// pass runs.
/// </summary>
public class InstalledTemplateParityTests
{
    private static readonly string[] MattDerivedSkills =
        ["wayfinder", "grilling", "grill-me", "bro", "writing-for-agents"];

    [Fact]
    public void MattDerivedTemplates_ShippedSourceEqualsInstalledCopy()
    {
        var root = FindRepositoryRoot();

        foreach (var skill in MattDerivedSkills)
        {
            var fileName = $"skill-{skill}.template.md";
            var shippedPath = Path.Combine(root, "Templates", fileName);
            var installedPath = Path.Combine(root, "dydo", "_system", "templates", fileName);

            Assert.True(File.Exists(shippedPath), $"Missing shipped source for {skill}");
            Assert.True(File.Exists(installedPath), $"Missing installed copy for {skill}");
            Assert.Equal(
                Normalize(File.ReadAllText(shippedPath)),
                Normalize(File.ReadAllText(installedPath)));
        }
    }

    private static string Normalize(string value) => value.Replace("\r\n", "\n");

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "DynaDocs.sln")) &&
                Directory.Exists(Path.Combine(directory.FullName, "Templates")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the DynaDocs repository root.");
    }
}
