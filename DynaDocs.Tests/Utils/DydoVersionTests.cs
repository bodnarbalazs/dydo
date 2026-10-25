// @test-tier: 2
namespace DynaDocs.Tests.Utils;

using System.Reflection;
using DynaDocs.Utils;

public class DydoVersionTests
{
    [Fact]
    public void Current_IsTheCliAssemblyInformationalVersion()
    {
        var expected = typeof(DydoVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        Assert.Equal(expected, DydoVersion.Current);
    }

    [Fact]
    public void Current_IsAPlainReleaseString()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", DydoVersion.Current);
    }
}
