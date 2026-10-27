namespace DynaDocs.Tests;

using System.Text;

/// <summary>
/// Assertions that a rewrite of a user's file changed only what it meant to change.
/// </summary>
internal static class PreservedText
{
    /// <summary>The file's text exactly as stored: a BOM stays as U+FEFF, line endings as written.</summary>
    public static string Read(string path) => Encoding.UTF8.GetString(File.ReadAllBytes(path));

    public static bool HasBom(string path) => File.ReadAllBytes(path) is [0xEF, 0xBB, 0xBF, ..];

    /// <summary>
    /// Every original line survives, in order. A line may only gain the trailing comma JSON needs
    /// when a member is added after it. Lines compare without their line ending, so an LF or CRLF
    /// checkout of an inline fixture reads the same.
    /// </summary>
    public static void AssertLinesKeptInOrder(string original, string result)
    {
        var lines = Lines(result);
        var next = 0;
        foreach (var line in Lines(original))
        {
            var at = Array.FindIndex(lines, next, candidate => candidate == line || candidate == line + ",");
            Assert.True(at >= 0, $"Original line lost or changed: {line}");
            next = at + 1;
        }
    }

    private static string[] Lines(string text) => text.Split('\n').Select(line => line.TrimEnd('\r')).ToArray();
}
