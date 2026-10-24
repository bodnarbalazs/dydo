namespace DynaDocs.Tests;

using System.Diagnostics;

internal static class DirectoryLinks
{
    /// <summary>A junction on Windows (no developer mode or elevation needed), a symlink elsewhere.</summary>
    public static void Create(string link, string target)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(link)!);
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(link, target);
            return;
        }

        using var mklink = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{link}\" \"{target}\"")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        })!;
        mklink.WaitForExit();
        Assert.Equal(0, mklink.ExitCode);
        Assert.True(new DirectoryInfo(link).Attributes.HasFlag(FileAttributes.ReparsePoint));
    }
}
