namespace DynaDocs.Tests.Sync.Notion;

using System.Text.RegularExpressions;
using DynaDocs.Sync;
using DynaDocs.Sync.Notion;

/// <summary>
/// Guards the hard invariant behind ns-6's converter restructure: <c>FromBlocks∘ToBlocks</c> must be a fixed point
/// on every already-synced record body, or the first sync after this change silently rewrites canonical files. The
/// sweep runs the real repo's seven sync-model dirs; the crafted cases pin the three divergence classes the old
/// line converter never hit (setext promotion, indented-code fencing, numbered renumbering).
/// </summary>
public class NotionBodyFixedPointTests
{
    private static string Norm(string body) =>
        NotionBlockConverter.FromBlocks(NotionBlockConverter.ToBlocks(body));

    [Fact]
    public void EverySyncedRecordBody_NormalizesIdempotently()
    {
        var root = FindRepoRoot();
        var model = File.ReadAllText(Path.Combine(root, "dydo", "_system", "sync-model.json"));
        var dirs = Regex.Matches(model, "\"dir\"\\s*:\\s*\"([^\"]+)\"").Select(m => m.Groups[1].Value).ToList();
        Assert.NotEmpty(dirs);

        var drifting = new List<string>();
        var swept = 0;
        foreach (var dir in dirs)
        {
            var full = Path.Combine(root, "dydo", dir);
            if (!Directory.Exists(full))
                continue;
            foreach (var file in Directory.EnumerateFiles(full, "*.md", SearchOption.AllDirectories))
            {
                var body = SyncDocFile.Read(file, "probe", file).Body;
                swept++;
                var once = Norm(body);
                if (once != Norm(once))
                    drifting.Add(Path.GetRelativePath(root, file));
            }
        }

        Assert.True(swept > 0, "swept no record bodies — repo root resolution is wrong");
        Assert.True(drifting.Count == 0,
            $"norm is not a fixed point on {drifting.Count} record body/ies — each contains a shape that does not "
            + "round-trip through the Notion converter (see the fixed-point notes in NotionBlockConverter):\n"
            + string.Join("\n", drifting));
    }

    [Theory]
    [InlineData("para above a rule\n---\nmore text")]        // setext OFF: the --- stays with the paragraph, never a heading
    [InlineData("    code line 1\n    code line 2")]           // an indented code block stays verbatim, never a fence
    [InlineData("3. three\n4. four")]                          // an ordered run not starting at 1 stays verbatim
    [InlineData("1. one\n3. three")]                           // a 1..n gap stays verbatim (would renumber otherwise)
    [InlineData("- a\n  - b\n    - c")]                        // a real nested bullet hierarchy round-trips
    [InlineData("1. one\n2. two\n3. three")]                   // a clean 1..n run round-trips as numbered items
    public void OldConverterEchoShapes_AreFixedPoints(string body)
    {
        Assert.Equal(body, Norm(body));
    }

    [Fact]
    public void NonSequentialOrderedItemWithNestedChild_ConvergesAfterTwoNormalizations()
    {
        // KNOWN EDGE (see NotionBlockConverter.MarkerWidth): a non-1..n ordered item renders verbatim as a
        // paragraph, so its child indents to the default width 2, under the 3-wide "3. " marker. The child is
        // under-indented and un-nests one level on re-parse — so this shape is NOT a pass-1 fixed point, but it
        // converges by the second normalization. No synced record hits it; this pins the behavior as conscious.
        const string body = "3. text\n   - sub";
        var once = Norm(body);
        var twice = Norm(once);
        var thrice = Norm(twice);

        Assert.NotEqual(once, twice);              // the sub-item un-nests one level on the first re-parse
        Assert.Equal(twice, thrice);               // converged: norm∘norm is a fixed point
        Assert.Equal("3. text\n- sub", twice);     // the sub-item is now a top-level sibling, not a child
    }

    [Fact]
    public void ThematicBreakWithBlankLines_NormalizesToAFixedPoint()
    {
        // A blank-separated horizontal rule loses its blanks on first norm, bringing --- adjacent to the paragraph
        // above; the second norm must not then promote it (setext) — it stabilizes instead.
        const string body = "intro paragraph\n\n---\n\nnext paragraph";
        var once = Norm(body);
        Assert.Equal(once, Norm(once));
        Assert.DoesNotContain("## ", once); // never promoted to a heading
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "dydo", "_system", "sync-model.json")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new DirectoryNotFoundException("could not locate repo root (dydo/_system/sync-model.json)");
    }
}
