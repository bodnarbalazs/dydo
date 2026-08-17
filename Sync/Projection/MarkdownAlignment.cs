namespace DynaDocs.Sync.Projection;

/// <summary>Bounded sibling alignment. A node has zero, one, or many base correspondences.</summary>
public sealed class MarkdownAlignment
{
    private const int PartitionLimit = 256;
    private readonly Dictionary<int, int> externalToLocal;
    private readonly HashSet<int> many;

    private MarkdownAlignment(Dictionary<int, int> externalToLocal, HashSet<int> many, string? conflict)
    {
        this.externalToLocal = externalToLocal;
        this.many = many;
        Conflict = conflict;
    }

    public string? Conflict { get; }

    public int Multiplicity(int externalIndex) => many.Contains(externalIndex) ? 2 : externalToLocal.ContainsKey(externalIndex) ? 1 : 0;

    public bool TryMap(int externalIndex, out int localIndex) => externalToLocal.TryGetValue(externalIndex, out localIndex);

    public static MarkdownAlignment Create(IReadOnlyList<MarkdownSyntaxNode> local, IReadOnlyList<MarkdownSyntaxNode> external)
    {
        var localCounts = Counts(local.Select(node => node.Identity));
        var externalCounts = Counts(external.Select(node => node.Identity));
        var sequence = DiffNodes(external, local);
        if (sequence.Conflict is not null)
            return new([], [], sequence.Conflict);

        var map = new Dictionary<int, int>();
        var many = new HashSet<int>();
        for (var externalIndex = 0; externalIndex < external.Count; externalIndex++)
        {
            var identity = external[externalIndex].Identity;
            if (localCounts.GetValueOrDefault(identity) != 1 || externalCounts.GetValueOrDefault(identity) != 1)
            {
                if (localCounts.ContainsKey(identity))
                    many.Add(externalIndex);
                continue;
            }
            if (sequence.BaseToCurrent.TryGetValue(externalIndex, out var localIndex))
                map[externalIndex] = localIndex;
        }
        return new(map, many, null);
    }

    internal static SequenceDiff DiffNodes(IReadOnlyList<MarkdownSyntaxNode> before, IReadOnlyList<MarkdownSyntaxNode> after) =>
        Diff(before, after, node => node.Identity, "Markdown diff partition");

    internal static TextDiff DiffText(string before, string after)
    {
        var beforeTokens = Tokens(before);
        var afterTokens = Tokens(after);
        var diff = Diff(beforeTokens, afterTokens, token => token.Value, "semantic leaf diff partition");
        if (diff.Conflict is not null)
            return new([], diff.Conflict);
        var changes = diff.Changes.Select(change => new SequenceChange(
            CharacterOffset(beforeTokens, change.BaseStart, before.Length), CharacterOffset(beforeTokens, change.BaseEnd, before.Length),
            CharacterOffset(afterTokens, change.CurrentStart, after.Length), CharacterOffset(afterTokens, change.CurrentEnd, after.Length))).ToArray();
        return new(changes, null);
    }

    internal static TextDiff DiffCharacters(string before, string after)
    {
        var diff = Diff(before.ToCharArray(), after.ToCharArray(), value => value.ToString(), "semantic character diff partition");
        return new(diff.Changes, diff.Conflict);
    }

    private static SequenceDiff Diff<T>(IReadOnlyList<T> before, IReadOnlyList<T> after, Func<T, string> identity, string label)
    {
        var beforeIds = before.Select(identity).ToArray();
        var afterIds = after.Select(identity).ToArray();
        var anchors = UniqueAnchors(beforeIds, afterIds);
        var matches = new List<(int Base, int Current)>();
        var baseStart = 0;
        var currentStart = 0;
        foreach (var anchor in anchors.Append((Base: beforeIds.Length, Current: afterIds.Length)))
        {
            if (anchor.Base - baseStart > PartitionLimit || anchor.Current - currentStart > PartitionLimit)
                return SequenceDiff.Failed($"{label} exceeds the {PartitionLimit}-node projection limit");
            matches.AddRange(Myers(beforeIds, baseStart, anchor.Base, afterIds, currentStart, anchor.Current));
            if (anchor.Base < beforeIds.Length)
                matches.Add(anchor);
            baseStart = anchor.Base + 1;
            currentStart = anchor.Current + 1;
        }
        matches.Sort((left, right) => left.Base.CompareTo(right.Base));
        var map = matches.ToDictionary(match => match.Base, match => match.Current);
        return new(Changes(matches, beforeIds.Length, afterIds.Length), map, null);
    }

    private static IReadOnlyList<(int Base, int Current)> UniqueAnchors(IReadOnlyList<string> before, IReadOnlyList<string> after)
    {
        var beforeCounts = Counts(before);
        var afterCounts = Counts(after);
        var afterIndexes = after.Select((value, index) => (value, index)).Where(pair =>
                beforeCounts.GetValueOrDefault(pair.value) == 1 && afterCounts.GetValueOrDefault(pair.value) == 1)
            .ToDictionary(pair => pair.value, pair => pair.index);
        var candidates = before.Select((value, index) => (Base: index, Current: afterIndexes.GetValueOrDefault(value, -1)))
            .Where(pair => pair.Current >= 0).ToArray();
        return LongestIncreasingSubsequence(candidates);
    }

    private static IReadOnlyList<(int Base, int Current)> LongestIncreasingSubsequence(IReadOnlyList<(int Base, int Current)> candidates)
    {
        var tails = new List<int>();
        var previous = new int[candidates.Count];
        Array.Fill(previous, -1);
        for (var index = 0; index < candidates.Count; index++)
        {
            var low = 0;
            var high = tails.Count;
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (candidates[tails[middle]].Current < candidates[index].Current)
                    low = middle + 1;
                else
                    high = middle;
            }
            if (low > 0)
                previous[index] = tails[low - 1];
            if (low == tails.Count)
                tails.Add(index);
            else
                tails[low] = index;
        }
        var result = new List<(int Base, int Current)>();
        for (var index = tails.Count == 0 ? -1 : tails[^1]; index >= 0; index = previous[index])
            result.Add(candidates[index]);
        result.Reverse();
        return result;
    }

    // Myers keeps one frontier per edit distance; it never allocates an N x M matrix.
    private static IReadOnlyList<(int Base, int Current)> Myers(IReadOnlyList<string> before, int beforeStart, int beforeEnd,
        IReadOnlyList<string> after, int afterStart, int afterEnd)
    {
        var n = beforeEnd - beforeStart;
        var m = afterEnd - afterStart;
        var offset = n + m + 1;
        var frontier = new int[2 * offset + 1];
        var trace = new List<int[]>();
        frontier[offset + 1] = 0;
        for (var distance = 0; distance <= n + m; distance++)
        {
            for (var diagonal = -distance; diagonal <= distance; diagonal += 2)
            {
                var index = offset + diagonal;
                var x = diagonal == -distance || diagonal != distance && frontier[index - 1] < frontier[index + 1]
                    ? frontier[index + 1]
                    : frontier[index - 1] + 1;
                var y = x - diagonal;
                while (x < n && y < m && before[beforeStart + x] == after[afterStart + y])
                {
                    x++;
                    y++;
                }
                frontier[index] = x;
                if (x < n || y < m)
                    continue;
                trace.Add((int[])frontier.Clone());
                return Backtrack(trace, n, m, offset, beforeStart, afterStart);
            }
            trace.Add((int[])frontier.Clone());
        }
        throw new InvalidOperationException("bounded Myers diff did not terminate");
    }

    private static IReadOnlyList<(int Base, int Current)> Backtrack(IReadOnlyList<int[]> trace, int n, int m, int offset,
        int beforeStart, int afterStart)
    {
        var result = new List<(int Base, int Current)>();
        var x = n;
        var y = m;
        for (var distance = trace.Count - 1; distance > 0; distance--)
        {
            var previous = trace[distance - 1];
            var diagonal = x - y;
            var previousDiagonal = diagonal == -distance || diagonal != distance
                && previous[offset + diagonal - 1] < previous[offset + diagonal + 1] ? diagonal + 1 : diagonal - 1;
            var previousX = previous[offset + previousDiagonal];
            var previousY = previousX - previousDiagonal;
            while (x > previousX && y > previousY)
            {
                result.Add((beforeStart + x - 1, afterStart + y - 1));
                x--;
                y--;
            }
            if (x == previousX)
                y--;
            else
                x--;
        }
        while (x > 0 && y > 0)
        {
            result.Add((beforeStart + x - 1, afterStart + y - 1));
            x--;
            y--;
        }
        result.Reverse();
        return result;
    }

    private static IReadOnlyList<SequenceChange> Changes(IReadOnlyList<(int Base, int Current)> matches, int beforeCount, int afterCount)
    {
        var changes = new List<SequenceChange>();
        var baseCursor = 0;
        var currentCursor = 0;
        foreach (var match in matches.Append((Base: beforeCount, Current: afterCount)))
        {
            if (baseCursor != match.Base || currentCursor != match.Current)
                changes.Add(new(baseCursor, match.Base, currentCursor, match.Current));
            baseCursor = match.Base + 1;
            currentCursor = match.Current + 1;
        }
        return changes;
    }

    private static Dictionary<string, int> Counts(IEnumerable<string> values) => values.GroupBy(value => value).ToDictionary(group => group.Key, group => group.Count());

    private static IReadOnlyList<TextToken> Tokens(string text)
    {
        var result = new List<TextToken>();
        for (var start = 0; start < text.Length;)
        {
            var whitespace = char.IsWhiteSpace(text[start]);
            var end = start + 1;
            while (end < text.Length && char.IsWhiteSpace(text[end]) == whitespace)
                end++;
            result.Add(new(text[start..end], start, end));
            start = end;
        }
        return result;
    }

    private static int CharacterOffset(IReadOnlyList<TextToken> tokens, int index, int length) => index == tokens.Count ? length : tokens[index].Start;

    internal sealed record SequenceDiff(IReadOnlyList<SequenceChange> Changes, IReadOnlyDictionary<int, int> BaseToCurrent, string? Conflict)
    {
        public static SequenceDiff Failed(string conflict) => new([], new Dictionary<int, int>(), conflict);
    }

    internal readonly record struct SequenceChange(int BaseStart, int BaseEnd, int CurrentStart, int CurrentEnd)
    {
        public bool Touches(int index) => BaseStart <= index && index < BaseEnd;
    }

    internal sealed record TextDiff(IReadOnlyList<SequenceChange> Changes, string? Conflict);
    private readonly record struct TextToken(string Value, int Start, int End);
}
