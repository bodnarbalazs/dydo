namespace DynaDocs.Sync.Projection;

/// <summary>Projects external Markdown edits onto untouched local source spans.</summary>
internal static class MarkdownPatchPlanner
{
    public static ProjectedBodyResult Merge(DualBodyBase bodyBase, string currentLocal, string currentExternal, string? pageTitle = null) =>
        Merge(bodyBase.LocalBody, bodyBase.ExternalBody, currentLocal, currentExternal, pageTitle);

    public static ProjectedBodyResult Merge(string localBase, string externalBase, string currentLocal, string currentExternal,
        string? pageTitle = null)
    {
        if (!MarkdownSyntaxNode.TryParse(localBase, pageTitle, out var localBaseNodes, out var reason)
            || !MarkdownSyntaxNode.TryParse(externalBase, null, out var externalBaseNodes, out reason)
            || !MarkdownSyntaxNode.TryParse(currentLocal, pageTitle, out var currentLocalNodes, out reason)
            || !MarkdownSyntaxNode.TryParse(currentExternal, null, out var currentExternalNodes, out reason))
            return ProjectedBodyResult.Failed(reason!);

        var state = new MergeState(localBase, externalBase, currentLocal, currentExternal,
            localBaseNodes.Count == 0 && currentLocalNodes.Count == 0 && !string.IsNullOrEmpty(pageTitle));
        if (!MergeSiblings(state, localBaseNodes, externalBaseNodes, currentLocalNodes, currentExternalNodes, true, 0,
                out var replacements, out reason))
            return ProjectedBodyResult.Failed(reason!);
        if (Overlaps(replacements))
            return ProjectedBodyResult.Failed("external changes map to overlapping local source spans");

        var merged = currentLocal;
        foreach (var replacement in replacements.OrderByDescending(replacement => replacement.Start))
            merged = merged[..replacement.Start] + replacement.Text + merged[replacement.End..];
        return ProjectedBodyResult.Success(merged);
    }

    private static bool MergeSiblings(MergeState state, IReadOnlyList<MarkdownSyntaxNode> localBase,
        IReadOnlyList<MarkdownSyntaxNode> externalBase, IReadOnlyList<MarkdownSyntaxNode> localCurrent,
        IReadOnlyList<MarkdownSyntaxNode> externalCurrent, bool root, int depth, out List<Replacement> replacements, out string? reason)
    {
        replacements = [];
        reason = null;
        if (depth > 256)
        {
            reason = "Markdown nesting exceeds the 256-level projection limit";
            return false;
        }
        var alignment = MarkdownAlignment.Create(localBase, externalBase);
        var localDiff = MarkdownAlignment.DiffNodes(localBase, localCurrent);
        var externalDiff = MarkdownAlignment.DiffNodes(externalBase, externalCurrent);
        var conflict = alignment.Conflict ?? localDiff.Conflict ?? externalDiff.Conflict;
        if (conflict is not null)
        {
            reason = conflict;
            return false;
        }

        foreach (var externalChange in externalDiff.Changes)
            if (!TryApplyChange(state, localBase, externalBase, localCurrent, externalCurrent, alignment, localDiff, externalChange,
                    root, depth, replacements, out reason))
                return false;
        return true;
    }

    private static bool TryApplyChange(MergeState state, IReadOnlyList<MarkdownSyntaxNode> localBase,
        IReadOnlyList<MarkdownSyntaxNode> externalBase, IReadOnlyList<MarkdownSyntaxNode> localCurrent,
        IReadOnlyList<MarkdownSyntaxNode> externalCurrent, MarkdownAlignment alignment, MarkdownAlignment.SequenceDiff localDiff,
        MarkdownAlignment.SequenceChange externalChange, bool root, int depth, List<Replacement> replacements, out string? reason)
    {
        reason = null;
        if (IsPairedChange(externalChange, alignment, localDiff, out var localBaseIndex, out var localCurrentIndex))
            return TryApplyPairedChange(state, localBase[localBaseIndex], externalBase[externalChange.BaseStart], localCurrent[localCurrentIndex],
                externalCurrent[externalChange.CurrentStart], depth, replacements, out reason);
        return TryApplyStructuralChange(state, externalBase, localCurrent, externalCurrent, alignment, localDiff, externalChange, root,
            replacements, out reason);
    }

    private static bool IsPairedChange(MarkdownAlignment.SequenceChange change, MarkdownAlignment alignment,
        MarkdownAlignment.SequenceDiff localDiff, out int localBaseIndex, out int localCurrentIndex)
    {
        localBaseIndex = localCurrentIndex = 0;
        return change.BaseEnd - change.BaseStart == 1 && change.CurrentEnd - change.CurrentStart == 1
            && alignment.Multiplicity(change.BaseStart) == 1 && alignment.TryMap(change.BaseStart, out localBaseIndex)
            && TryCurrentIndex(localDiff, localBaseIndex, out localCurrentIndex);
    }

    private static bool TryApplyPairedChange(MergeState state, MarkdownSyntaxNode localBase, MarkdownSyntaxNode externalBase,
        MarkdownSyntaxNode localCurrent, MarkdownSyntaxNode externalCurrent, int depth, List<Replacement> replacements, out string? reason)
    {
        if (!MergeNode(state, localBase, externalBase, localCurrent, externalCurrent, depth + 1, out var nested, out reason))
            return false;
        replacements.AddRange(nested);
        return true;
    }

    private static bool TryApplyStructuralChange(MergeState state, IReadOnlyList<MarkdownSyntaxNode> externalBase,
        IReadOnlyList<MarkdownSyntaxNode> localCurrent, IReadOnlyList<MarkdownSyntaxNode> externalCurrent, MarkdownAlignment alignment,
        MarkdownAlignment.SequenceDiff localDiff, MarkdownAlignment.SequenceChange change, bool root, List<Replacement> replacements,
        out string? reason)
    {
        if (!TryMapRange(change, alignment, localDiff, localCurrent, out var localStart, out var localEnd, out reason))
            return false;
        if (TouchesLocalChange(change, alignment, localDiff))
        {
            reason = "local and external changes overlap on the same Markdown region";
            return false;
        }
        if (change.BaseStart != change.BaseEnd)
        {
            replacements.Add(new(localStart, localEnd, CurrentRaw(state.ExternalCurrent, externalCurrent, change.CurrentStart, change.CurrentEnd)));
            return true;
        }
        if (!TryInsertionPosition(change, alignment, localDiff, localCurrent, state, out var position, out reason))
            return false;
        if (root && change.BaseStart > 0 && change.BaseStart < externalBase.Count
            && TryRootGap(change, alignment, localDiff, localCurrent, out var gapStart, out var gapEnd))
        {
            replacements.Add(new(gapStart, gapEnd, state.LocalCurrent[gapStart..gapEnd] + CurrentRaw(state.ExternalCurrent,
                externalCurrent, change.CurrentStart, change.CurrentEnd).Trim('\n') + "\n\n"));
            return true;
        }
        var text = root ? BoundaryInsert(CurrentRaw(state.ExternalCurrent, externalCurrent, change.CurrentStart, change.CurrentEnd),
            state.LocalCurrent, position) : NestedInsert(state.ExternalCurrent, externalCurrent, change, position);
        replacements.Add(new(position, position, text));
        return true;
    }

    private static bool TryRootGap(MarkdownAlignment.SequenceChange change, MarkdownAlignment alignment,
        MarkdownAlignment.SequenceDiff localDiff, IReadOnlyList<MarkdownSyntaxNode> localCurrent, out int start, out int end)
    {
        start = end = 0;
        if (!alignment.TryMap(change.BaseStart - 1, out var leftBase) || !alignment.TryMap(change.BaseStart, out var rightBase)
            || !TryCurrentIndex(localDiff, leftBase, out var leftCurrent) || !TryCurrentIndex(localDiff, rightBase, out var rightCurrent))
            return false;
        start = localCurrent[leftCurrent].End;
        end = localCurrent[rightCurrent].Start;
        return true;
    }

    private static bool MergeNode(MergeState state, MarkdownSyntaxNode localBase, MarkdownSyntaxNode externalBase,
        MarkdownSyntaxNode localCurrent, MarkdownSyntaxNode externalCurrent, int depth, out List<Replacement> replacements, out string? reason)
    {
        replacements = [];
        reason = null;
        if (SyntaxChanged(localBase, externalBase, localCurrent, externalCurrent))
        {
            if (localBase.Identity != localCurrent.Identity)
            {
                reason = "local and external changes overlap on the same Markdown syntax";
                return false;
            }
            replacements.Add(new(localCurrent.Start, localCurrent.End, Raw(state.ExternalCurrent, externalCurrent).Replace("\r\n", "\n")));
            return true;
        }
        if (localBase.Kind == "TableRow" && Raw(state.LocalBase, localBase) == Raw(state.ExternalBase, externalBase)
            && Raw(state.LocalBase, localBase) == Raw(state.LocalCurrent, localCurrent))
        {
            replacements.Add(new(localCurrent.Start, localCurrent.End, Raw(state.ExternalCurrent, externalCurrent).Replace("\r\n", "\n")));
            return true;
        }
        if (localBase.Kind == "TableCell")
            return MergeText(state, localBase, externalBase, localCurrent, externalCurrent, out replacements, out reason);
        if (localBase.Children.Count > 0 || externalBase.Children.Count > 0 || localCurrent.Children.Count > 0 || externalCurrent.Children.Count > 0)
        {
            if (localBase.Children.Count == 0 || externalBase.Children.Count == 0 || localCurrent.Children.Count == 0 || externalCurrent.Children.Count == 0)
            {
                reason = "external change has an incompatible Markdown child structure";
                return false;
            }
            return MergeSiblings(state, localBase.Children, externalBase.Children, localCurrent.Children, externalCurrent.Children,
                false, depth, out replacements, out reason);
        }
        return MergeText(state, localBase, externalBase, localCurrent, externalCurrent, out replacements, out reason);
    }

    private static bool MergeText(MergeState state, MarkdownSyntaxNode localBase, MarkdownSyntaxNode externalBase,
        MarkdownSyntaxNode localCurrent, MarkdownSyntaxNode externalCurrent, out List<Replacement> replacements, out string? reason)
    {
        replacements = [];
        reason = null;
        var externalBefore = Map(state.ExternalBase, externalBase);
        var externalAfter = Map(state.ExternalCurrent, externalCurrent);
        var localBefore = Map(state.LocalBase, localBase);
        var localAfter = Map(state.LocalCurrent, localCurrent);
        if (externalBefore.Text != localBefore.Text)
        {
            reason = "external change has no matching semantic leaf";
            return false;
        }
        var externalDiff = MarkdownAlignment.DiffText(externalBefore.Text, externalAfter.Text);
        var localDiff = MarkdownAlignment.DiffText(localBefore.Text, localAfter.Text);
        var conflict = externalDiff.Conflict ?? localDiff.Conflict;
        if (conflict is not null)
        {
            reason = conflict.Replace("node", "token", StringComparison.Ordinal);
            return false;
        }
        foreach (var externalChange in externalDiff.Changes)
        {
            var overlaps = localDiff.Changes.Where(localChange => TextOverlaps(externalChange, localChange)).ToArray();
            if (overlaps.Length > 0)
            {
                if (!TryFineComposition(state, externalBefore, externalAfter, localBefore, localAfter, externalChange, overlaps,
                        externalCurrent, localCurrent, out var fine, out reason))
                    return false;
                replacements.AddRange(fine);
                continue;
            }
            AddTextReplacement(replacements, state, externalAfter, localAfter, externalChange, localDiff.Changes, localCurrent, externalCurrent,
                out reason);
            if (reason is not null)
                return false;
        }
        return true;
    }

    private static bool TryFineComposition(MergeState state, SemanticTextMap externalBefore, SemanticTextMap externalAfter,
        SemanticTextMap localBefore, SemanticTextMap localAfter, MarkdownAlignment.SequenceChange externalChange,
        IReadOnlyList<MarkdownAlignment.SequenceChange> localChanges, MarkdownSyntaxNode externalCurrent, MarkdownSyntaxNode localCurrent,
        out List<Replacement> replacements, out string? reason)
    {
        replacements = [];
        reason = "local and external changes overlap on the same semantic leaf range";
        if (localChanges.Count != 1 || localChanges[0].BaseStart != externalChange.BaseStart || localChanges[0].BaseEnd != externalChange.BaseEnd
            || externalChange.BaseEnd - externalChange.BaseStart > 256)
            return false;
        var baseText = externalBefore.Text[externalChange.BaseStart..externalChange.BaseEnd];
        var externalText = externalAfter.Text[externalChange.CurrentStart..externalChange.CurrentEnd];
        var localText = localAfter.Text[localChanges[0].CurrentStart..localChanges[0].CurrentEnd];
        if (baseText.Any(char.IsWhiteSpace) || externalText.Any(char.IsWhiteSpace) || localText.Any(char.IsWhiteSpace))
            return false;
        var externalDiff = MarkdownAlignment.DiffCharacters(baseText, externalText);
        var localDiff = MarkdownAlignment.DiffCharacters(baseText, localText);
        var conflict = externalDiff.Conflict ?? localDiff.Conflict;
        if (conflict is not null)
        {
            reason = conflict;
            return false;
        }
        foreach (var change in externalDiff.Changes)
        {
            if (localDiff.Changes.Any(localChange => TextOverlaps(change, localChange)))
                return false;
            var shifted = new MarkdownAlignment.SequenceChange(externalChange.BaseStart + change.BaseStart,
                externalChange.BaseStart + change.BaseEnd, externalChange.CurrentStart + change.CurrentStart,
                externalChange.CurrentStart + change.CurrentEnd);
            var translatedLocalChanges = localDiff.Changes.Select(localChange => new MarkdownAlignment.SequenceChange(
                externalChange.BaseStart + localChange.BaseStart, externalChange.BaseStart + localChange.BaseEnd,
                localChanges[0].CurrentStart + localChange.CurrentStart, localChanges[0].CurrentStart + localChange.CurrentEnd)).ToArray();
            AddTextReplacement(replacements, state, externalAfter, localAfter, shifted, translatedLocalChanges, localCurrent, externalCurrent,
                out reason);
            if (reason is not null)
                return false;
        }
        return true;
    }

    private static void AddTextReplacement(List<Replacement> replacements, MergeState state, SemanticTextMap externalAfter,
        SemanticTextMap localAfter, MarkdownAlignment.SequenceChange externalChange,
        IReadOnlyList<MarkdownAlignment.SequenceChange> localChanges, MarkdownSyntaxNode localCurrent, MarkdownSyntaxNode externalCurrent,
        out string? reason)
    {
        reason = null;
        var shift = localChanges.Where(change => change.BaseEnd <= externalChange.BaseStart)
            .Sum(change => change.CurrentEnd - change.CurrentStart - (change.BaseEnd - change.BaseStart));
        var localStart = externalChange.BaseStart + shift;
        var localEnd = externalChange.BaseEnd + shift;
        if (localStart < 0 || localEnd < localStart || localEnd > localAfter.Text.Length)
        {
            reason = "external change maps to an invalid local semantic leaf range";
            return;
        }
        var start = RawStart(localAfter, localStart, localCurrent.End);
        var end = RawEnd(localAfter, localEnd, start);
        var externalStart = RawStart(externalAfter, externalChange.CurrentStart, externalCurrent.End);
        var externalEnd = RawEnd(externalAfter, externalChange.CurrentEnd, externalStart);
        var text = externalCurrent.Kind == "TableCell"
            ? externalAfter.Text[externalChange.CurrentStart..externalChange.CurrentEnd]
            : state.ExternalCurrent[externalStart..externalEnd];
        replacements.Add(new(start, end, text));
    }

    private static bool TryMapRange(MarkdownAlignment.SequenceChange change, MarkdownAlignment alignment,
        MarkdownAlignment.SequenceDiff localDiff, IReadOnlyList<MarkdownSyntaxNode> localCurrent, out int start, out int end, out string? reason)
    {
        start = end = 0;
        reason = null;
        if (change.BaseStart == change.BaseEnd)
            return true;
        var mapped = new List<int>();
        for (var index = change.BaseStart; index < change.BaseEnd; index++)
        {
            if (alignment.Multiplicity(index) != 1 || !alignment.TryMap(index, out var localBaseIndex))
            {
                reason = alignment.Multiplicity(index) > 1
                    ? "external change touches a repeated ambiguous Markdown region"
                    : "external change has no unique base alignment";
                return false;
            }
            if (!TryCurrentIndex(localDiff, localBaseIndex, out var localCurrentIndex))
            {
                reason = "local and external changes overlap on the same Markdown region";
                return false;
            }
            mapped.Add(localCurrentIndex);
        }
        if (mapped.Max() - mapped.Min() + 1 != mapped.Count)
        {
            reason = "external change crosses a non-contiguous base alignment";
            return false;
        }
        start = localCurrent[mapped.Min()].Start;
        end = localCurrent[mapped.Max()].End;
        return true;
    }

    private static bool SyntaxChanged(MarkdownSyntaxNode localBase, MarkdownSyntaxNode externalBase,
        MarkdownSyntaxNode localCurrent, MarkdownSyntaxNode externalCurrent) => localBase.Syntax != externalBase.Syntax
            || localBase.Syntax != localCurrent.Syntax || externalBase.Syntax != externalCurrent.Syntax;

    private static bool TryInsertionPosition(MarkdownAlignment.SequenceChange change, MarkdownAlignment alignment,
        MarkdownAlignment.SequenceDiff localDiff, IReadOnlyList<MarkdownSyntaxNode> localCurrent, MergeState state,
        out int position, out string? reason)
    {
        position = 0;
        reason = null;
        if (change.BaseStart == 0 && localCurrent.Count == 0)
        {
            position = state.LocalTitleOnly ? state.LocalCurrent.Length : 0;
            return true;
        }
        foreach (var externalIndex in new[] { change.BaseStart - 1, change.BaseStart })
            if (externalIndex >= 0 && externalIndex < localCurrent.Count + 1 && alignment.Multiplicity(externalIndex) > 1)
            {
                reason = "external insertion touches a repeated ambiguous Markdown region";
                return false;
            }
        if (change.BaseStart > 0 && alignment.TryMap(change.BaseStart - 1, out var leftBase)
            && TryCurrentIndex(localDiff, leftBase, out var leftCurrent))
        {
            position = localCurrent[leftCurrent].End;
            return true;
        }
        if (alignment.TryMap(change.BaseStart, out var rightBase) && TryCurrentIndex(localDiff, rightBase, out var rightCurrent))
        {
            position = localCurrent[rightCurrent].Start;
            return true;
        }
        reason = "external insertion has no unique neighboring base node";
        return false;
    }

    private static bool TryCurrentIndex(MarkdownAlignment.SequenceDiff diff, int baseIndex, out int currentIndex)
    {
        if (diff.BaseToCurrent.TryGetValue(baseIndex, out currentIndex))
            return true;
        var change = diff.Changes.SingleOrDefault(change => change.BaseStart == baseIndex && change.BaseEnd == baseIndex + 1
            && change.CurrentEnd == change.CurrentStart + 1);
        if (change.BaseEnd == 0)
        {
            currentIndex = 0;
            return false;
        }
        currentIndex = change.CurrentStart;
        return true;
    }

    private static bool TouchesLocalChange(MarkdownAlignment.SequenceChange externalChange, MarkdownAlignment alignment,
        MarkdownAlignment.SequenceDiff localDiff)
    {
        for (var externalIndex = externalChange.BaseStart; externalIndex < externalChange.BaseEnd; externalIndex++)
            if (alignment.TryMap(externalIndex, out var localIndex) && localDiff.Changes.Any(change => change.Touches(localIndex)))
                return true;
        return false;
    }

    private static bool TextOverlaps(MarkdownAlignment.SequenceChange left, MarkdownAlignment.SequenceChange right) =>
        left.BaseStart == left.BaseEnd || right.BaseStart == right.BaseEnd
            ? left.BaseStart <= right.BaseEnd && right.BaseStart <= left.BaseEnd
            : left.BaseStart < right.BaseEnd && right.BaseStart < left.BaseEnd;

    private static int RawStart(SemanticTextMap map, int index, int fallback) =>
        index == map.Text.Length ? map.Text.Length == 0 ? fallback : map.RawEnd(map.Text.Length - 1) : map.RawOffset(index);

    private static int RawEnd(SemanticTextMap map, int index, int fallback) => index == 0 ? fallback : map.RawEnd(index - 1);

    private static string CurrentRaw(string source, IReadOnlyList<MarkdownSyntaxNode> nodes, int start, int end) =>
        start == end ? "" : source[nodes[start].Start..nodes[end - 1].End].Replace("\r\n", "\n");

    private static string NestedInsert(string source, IReadOnlyList<MarkdownSyntaxNode> nodes, MarkdownAlignment.SequenceChange change, int position)
    {
        var start = nodes[change.CurrentStart].Start;
        if (change.CurrentStart > 0)
            start = nodes[change.CurrentStart - 1].End;
        return source[start..nodes[change.CurrentEnd - 1].End].Replace("\r\n", "\n");
    }

    private static string BoundaryInsert(string raw, string local, int position)
    {
        var content = raw.Trim('\n');
        if (content.Length == 0)
            return "";
        if (position == 0)
            return local.Length == 0 ? content : content + "\n\n";
        if (position == local.Length)
            return "\n\n" + content;
        return "\n\n" + content + "\n\n";
    }

    private static string Raw(string source, MarkdownSyntaxNode node) => source[node.Start..node.End];

    private static SemanticTextMap Map(string source, MarkdownSyntaxNode node) => node.Kind switch
    {
        "FencedCodeBlock" => SemanticTextMap.CreateFencedCode(Raw(source, node), node.Start),
        "CodeBlock" => SemanticTextMap.CreateRaw(Raw(source, node), node.Start),
        "TableCell" => SemanticTextMap.CreateTableCell(Raw(source, node), node.Start),
        _ => SemanticTextMap.Create(Raw(source, node), node.Start)
    };

    private static bool Overlaps(IReadOnlyList<Replacement> replacements)
    {
        var ordered = replacements.OrderBy(replacement => replacement.Start).ThenBy(replacement => replacement.End).ToArray();
        return ordered.Zip(ordered.Skip(1), (left, right) => left.End > right.Start || left.Start == left.End && right.Start == right.End
            && left.Start == right.Start).Any(value => value);
    }

    private sealed record MergeState(string LocalBase, string ExternalBase, string LocalCurrent, string ExternalCurrent, bool LocalTitleOnly);
    private readonly record struct Replacement(int Start, int End, string Text);
}
