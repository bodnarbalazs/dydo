namespace DynaDocs.Tests.Utils;

using DynaDocs.Utils;

public class DydoVersionTests
{
    [Fact]
    public void Current_IsAPlainReleaseString()
    {
        Assert.Matches(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", DydoVersion.Current);
    }
}
