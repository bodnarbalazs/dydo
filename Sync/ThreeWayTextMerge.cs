namespace DynaDocs.Sync;

/// <summary>
/// Line-based 3-way text merge against a common base. Chosen over shelling out to
/// <c>git merge-file</c> (Decision 025 §3 allows either): an in-process line merge needs no
/// temp files or subprocess and is fully deterministic under test. Non-overlapping edits merge
/// silently; a region both sides changed differently is a conflict — the result keeps a
/// deterministic winner (the repo side) wrapped in visible markers, never a silent clobber.
/// </summary>
public static class ThreeWayTextMerge
{
    public sealed class Result
    {
        public required string Text { get; init; }
        public required bool Conflicted { get; init; }
    }

    private const string OursLabel = "<<<<<<< repo";
    private const string MidLabel = "=======";
    private const string TheirsLabel = ">>>>>>> external";

    /// <summary>Whether a body carries this merge's conflict sentinels — the backstop the docs mirror's
    /// safety-rail invariant (DR 035 §5) checks before persisting a body to a canonical repo file: a body
    /// bearing markers is routed to the shadow tree, never written to the canonical file. A bare <c>=======</c>
    /// is deliberately excluded: it is byte-identical to a Markdown setext H1 underline and legitimate content, so
    /// treating it as a marker would permanently wedge fully resolved documents. Either endpoint sentinel catches every
    /// single-endpoint-deleted partial resolution; if both endpoints are deleted, the remaining mid-marker promotes
    /// visibly and can be fixed by a human rather than wedging the document.</summary>
    public static bool ContainsConflictMarkers(string text) =>
        text.Contains(OursLabel, StringComparison.Ordinal)
        || text.Contains(TheirsLabel, StringComparison.Ordinal);

    public static Result Merge(string baseText, string ours, string theirs)
    {
        var b = Split(baseText);
        var o = Split(ours);
        var t = Split(theirs);

        var oursDiff = Diff(b, o);
        var theirsDiff = Diff(b, t);

        var merged = new List<string>();
        var conflicted = false;
        int bi = 0; // index into base

        while (bi < b.Count)
        {
            var oChange = oursDiff.TryGetValue(bi, out var oc) ? oc : null;
            var tChange = theirsDiff.TryGetValue(bi, out var tc) ? tc : null;

            if (oChange == null && tChange == null)
            {
                merged.Add(b[bi]);
                bi++;
                continue;
            }

            var consumed = EmitRegion(merged, oursDiff, theirsDiff, bi, ref conflicted);

            // A pure insertion (consumed == 0) sits *before* the unchanged anchor base[bi];
            // emit that anchor and step past it so the loop always advances (else: infinite append).
            if (consumed == 0)
            {
                merged.Add(b[bi]);
                bi++;
            }
            else
            {
                bi += consumed;
            }
        }

        EmitTail(merged, oursDiff, theirsDiff, b.Count, ref conflicted);

        return new Result { Text = string.Join('\n', merged), Conflicted = conflicted };
    }

    /// <summary>
    /// Emit one merge region that begins at base index <paramref name="start"/> (at least one
    /// side has a change keyed there). A side's change at the region start may consume several
    /// base lines (a block rewrite/delete); the *other* side may have independent change entries
    /// whose keys fall *inside* that span. Those must not be skipped — we grow the region to
    /// cover every overlapping change entry from both sides and, if both sides touched the
    /// (combined) span, emit a conflict so neither side's content is silently lost. Returns the
    /// number of base lines consumed by the whole region.
    /// </summary>
    private static int EmitRegion(
        List<string> merged, Dictionary<int, Change> oursDiff, Dictionary<int, Change> theirsDiff,
        int start, ref bool conflicted)
    {
        var oChange = oursDiff.GetValueOrDefault(start);
        var tChange = theirsDiff.GetValueOrDefault(start);

        int end = start + Math.Max(oChange?.BaseLinesConsumed ?? 0, tChange?.BaseLinesConsumed ?? 0);
        var span = GrowRegion(oursDiff, theirsDiff, start, end);

        // Common fast path: a single change entry at `start` on exactly one side, nothing folded.
        if (!(span.OursTouched && span.TheirsTouched))
        {
            merged.AddRange((oChange ?? tChange)!.Replacement);
            return span.End - start;
        }

        CollectAndEmit(merged, oursDiff, theirsDiff, start, span.End, ref conflicted);
        return span.End - start;
    }

    private readonly record struct Region(int End, bool OursTouched, bool TheirsTouched);

    /// <summary>
    /// Grow <c>[start, end)</c> until it is closed: no change entry on either side keyed inside the
    /// span extends past <c>end</c>, and any change keyed inside the span has been folded in. A
    /// change entry (either side) keyed strictly inside the span overlaps the region a multi-line
    /// change opened, so it belongs to this region — never skip it. Reports whether *both* sides
    /// contributed a change anywhere in the final span.
    /// </summary>
    private static Region GrowRegion(
        Dictionary<int, Change> oursDiff, Dictionary<int, Change> theirsDiff, int start, int end)
    {
        bool oursTouched = oursDiff.ContainsKey(start);
        bool theirsTouched = theirsDiff.ContainsKey(start);

        bool grew = true;
        while (grew)
        {
            grew = false;
            for (var k = start + 1; k < end; k++)
            {
                grew |= Fold(oursDiff, k, ref end, ref oursTouched);
                grew |= Fold(theirsDiff, k, ref end, ref theirsTouched);
            }
        }

        return new Region(end, oursTouched, theirsTouched);
    }

    /// <summary>Fold a single side's change entry keyed at <paramref name="k"/> into the region,
    /// extending <paramref name="end"/> if it reaches past the current span. Returns whether the
    /// span grew.</summary>
    private static bool Fold(Dictionary<int, Change> diff, int k, ref int end, ref bool touched)
    {
        if (!diff.TryGetValue(k, out var c))
            return false;
        touched = true;
        var kend = k + c.BaseLinesConsumed;
        if (kend <= end)
            return false;
        end = kend;
        return true;
    }

    /// <summary>
    /// Both sides changed lines within the span — an overlap. Surface each side's content for the
    /// span by concatenating its change-entry replacements in base order. If the two sides happen
    /// to agree verbatim we emit it once (no spurious conflict); otherwise we wrap both in markers
    /// so neither side is silently lost (the brief's hard requirement).
    /// </summary>
    private static void CollectAndEmit(
        List<string> merged, Dictionary<int, Change> oursDiff, Dictionary<int, Change> theirsDiff,
        int start, int end, ref bool conflicted)
    {
        var ours = CollectReplacements(oursDiff, start, end);
        var theirs = CollectReplacements(theirsDiff, start, end);

        if (ours.SequenceEqual(theirs))
            merged.AddRange(ours);
        else
            conflicted |= EmitConflict(merged, ours, theirs);
    }

    /// <summary>
    /// Concatenate, in base order, the replacement lines of every change entry one side has keyed
    /// in this region. The region anchor <paramref name="start"/> is always included (a pure
    /// insertion is keyed there with zero consumed base lines, giving the degenerate
    /// <c>end == start</c> region); plus every entry keyed strictly inside <c>(start, end)</c>. The
    /// boundary entry at <c>end</c> is excluded — it begins the next region. Used only on the
    /// conflict path to surface that side's full content for the span. Base lines a side left
    /// unchanged are intentionally omitted — they reappear from the other side's region or are part
    /// of the spanning rewrite — so this is a faithful view of *what that side wrote* in the
    /// overlapping region, never a silent drop.
    /// </summary>
    private static List<string> CollectReplacements(Dictionary<int, Change> diff, int start, int end)
    {
        var lines = new List<string>();
        for (var k = start; k < end || k == start; k++)
        {
            if (diff.TryGetValue(k, out var c))
                lines.AddRange(c.Replacement);
            if (k == start && end == start) break; // degenerate insertion-only region: only the anchor
        }
        return lines;
    }

    /// <summary>Trailing inserts past the end of base (one or both sides append at EOF).</summary>
    private static void EmitTail(
        List<string> merged, Dictionary<int, Change> oursDiff, Dictionary<int, Change> theirsDiff,
        int baseCount, ref bool conflicted)
    {
        var oTail = oursDiff.GetValueOrDefault(baseCount);
        var tTail = theirsDiff.GetValueOrDefault(baseCount);
        if (oTail == null && tTail == null)
            return;

        if (oTail != null && tTail != null && !oTail.Replacement.SequenceEqual(tTail.Replacement))
            conflicted |= EmitConflict(merged, oTail.Replacement, tTail.Replacement);
        else
            merged.AddRange((oTail ?? tTail)!.Replacement);
    }

    /// <summary>Wrap two diverging side-replacements in visible conflict markers; never silent loss.</summary>
    private static bool EmitConflict(List<string> merged, List<string> ours, List<string> theirs)
    {
        merged.Add(OursLabel);
        merged.AddRange(ours);
        merged.Add(MidLabel);
        merged.AddRange(theirs);
        merged.Add(TheirsLabel);
        return true;
    }

    private sealed class Change
    {
        public required List<string> Replacement { get; init; }
        public required int BaseLinesConsumed { get; init; }
    }

    /// <summary>
    /// Diff base→side keyed by the base line index where each change region begins. Each region
    /// records the side's replacement lines and how many base lines it consumed. An append past
    /// EOF is keyed at <c>base.Count</c>. Uses an LCS so unchanged anchors align correctly.
    /// </summary>
    private static Dictionary<int, Change> Diff(List<string> b, List<string> s)
    {
        var lcs = LongestCommonSubsequence(b, s);
        var changes = new Dictionary<int, Change>();

        int bi = 0, si = 0, li = 0;
        while (bi < b.Count || si < s.Count)
        {
            var anchorB = li < lcs.Count ? lcs[li].BaseIndex : b.Count;
            var anchorS = li < lcs.Count ? lcs[li].SideIndex : s.Count;

            if (bi < anchorB || si < anchorS)
            {
                var replacement = s.GetRange(si, anchorS - si);
                changes[bi] = new Change { Replacement = replacement, BaseLinesConsumed = anchorB - bi };
                bi = anchorB;
                si = anchorS;
            }
            else
            {
                // Aligned anchor line — unchanged.
                bi++;
                si++;
                li++;
            }
        }

        return changes;
    }

    private readonly record struct Anchor(int BaseIndex, int SideIndex);

    private static List<Anchor> LongestCommonSubsequence(List<string> a, List<string> c)
    {
        var n = a.Count;
        var m = c.Count;
        var dp = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
            for (var j = m - 1; j >= 0; j--)
                dp[i, j] = a[i] == c[j] ? dp[i + 1, j + 1] + 1 : Math.Max(dp[i + 1, j], dp[i, j + 1]);

        var anchors = new List<Anchor>();
        int x = 0, y = 0;
        while (x < n && y < m)
        {
            if (a[x] == c[y])
            {
                anchors.Add(new Anchor(x, y));
                x++;
                y++;
            }
            else if (dp[x + 1, y] >= dp[x, y + 1]) x++;
            else y++;
        }
        return anchors;
    }

    private static List<string> Split(string text) =>
        text.Replace("\r\n", "\n").Split('\n').ToList();
}
