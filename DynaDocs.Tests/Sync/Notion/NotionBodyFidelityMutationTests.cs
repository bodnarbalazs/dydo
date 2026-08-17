namespace DynaDocs.Tests.Sync.Notion;

using DynaDocs.Models;
using DynaDocs.Sync;
using DynaDocs.Sync.Projection;

/// <summary>
/// The projection merger must copy authored source spans, not re-render a Markdown dialect.  These are intentionally
/// small, exact cases: a changed semantic token moves, while every other byte remains the spelling in the local file.
/// </summary>
public sealed class NotionBodyFidelityMutationTests
{
    [Fact]
    public void H1Omission_WithASecondSemanticChangeFailsClosed()
    {
        var result = ProjectedMarkdownMerge.Merge("# Slice 11\n\nalpha\n\nbeta\n", "alpha\n\nbeta\n",
            "# Slice 11\n\nalpha remote\n\nbeta\n", "alpha remote\n\nbeta\n", "Slice 11");

        Assert.Null(result.Body);
        Assert.NotNull(result.Conflict);
    }

    [Fact]
    public void AsymmetricH1Omission_EquivalentRepresentationsKeepAuthoredBytes() =>
        AssertAsymmetric("# Title\n\n**bold**\n\n- [ ] task\n", "**bold**\n\n- [ ] task\n", "Title");

    [Fact]
    public void AsymmetricEscapesAndBlankGaps_EquivalentRepresentationsKeepAuthoredBytes() =>
        AssertAsymmetric("before\\!\n\n\n\n[link](https://example.test)\n", "before!\n\n[link](https://example.test)\n", null);

    [Fact]
    public void AsymmetricTableAndList_EquivalentRepresentationsKeepAuthoredBytes() =>
        AssertAsymmetric("- parent\n  - child\n\n| A | B |\n| --- | --- |\n| one | two |\n", "* parent\n    * child\n\n| A | B |\n|---|---|\n|one|two|\n", null);

    [Fact]
    public void AsymmetricOneSidedLinkAndCheckboxEdit_RegistersSemanticExternalMutation()
    {
        const string local = "# Title\n\n[link](https://one.test)\n\n- [ ] task\n";
        var result = ProjectedMarkdownMerge.Merge(local, "[link](https://one.test)\n\n- [ ] task\n", local,
            "[link](https://two.test)\n\n- [x] task\n", "Title");

        Assert.Null(result.Conflict);
        Assert.Equal("# Title\n\n[link](https://two.test)\n\n- [x] task\n", result.Body);
    }

    [Fact]
    public void BlankGaps_ModificationPreservesAllUnchangedBlankLines() =>
        AssertMerged("before\n\n\n\nchange\n\n\nafter\n", "before\n\n\n\nremote change\n\n\nafter\n");

    [Fact]
    public void EscapedPunctuation_ModificationIsObservable() =>
        AssertMerged("before\\!\n\nvalue\\?\n\nafter\\.\n", "before\\!\n\nremote\\?\n\nafter\\.\n");

    [Fact]
    public void Emphasis_WordMutationKeepsTheLocalDelimiters() =>
        AssertMerged("before\n\n**bold** and _italic_\n\nafter\n", "before\n\n**remote** and _italic_\n\nafter\n");

    [Fact]
    public void LinkTarget_MutationIsObservable() =>
        AssertMerged("before\n\n[link](https://one.example/a)\n\nafter\n", "before\n\n[link](https://two.example/b)\n\nafter\n");

    [Fact]
    public void NestedList_LeafMutationPreservesSiblingBytes() =>
        AssertMerged("before\n\n- parent\n  - child\n  - sibling\n\nafter\n", "before\n\n- parent\n  - remote child\n  - sibling\n\nafter\n");

    [Fact]
    public void TableCell_MutationPreservesTableSyntax() =>
        AssertMerged("before\n\n| A | B |\n| --- | --- |\n| one | two |\n\nafter\n", "before\n\n| A | B |\n| --- | --- |\n| one | remote |\n\nafter\n");

    [Fact]
    public void Quote_MutationPreservesTheQuoteMarker() =>
        AssertMerged("before\n\n> quoted value\n\nafter\n", "before\n\n> remote value\n\nafter\n");

    [Fact]
    public void FencedCode_MutationIsObservableWithoutTouchingFence() =>
        AssertMerged("before\n\n```csharp\nvar value = 1;\n```\n\nafter\n", "before\n\n```csharp\nvar value = 2;\n```\n\nafter\n");

    [Fact]
    public void Checkbox_MutationIsObservable() =>
        AssertMerged("before\n\n- [ ] pending\n\nafter\n", "before\n\n- [x] pending\n\nafter\n");

    [Fact]
    public void Insert_AddsOneUniqueSectionAtTheBoundary() =>
        AssertMerged("before\n\nanchor\n\nafter\n", "before\n\nanchor\n\ninserted\n\nafter\n");

    [Fact]
    public void Delete_RemovesOnlyTheUniqueSection() =>
        AssertProjected("before\n\nremove me\n\nafter\n", "before\n\nafter\n", "before\n\n\n\nafter\n");

    [Fact]
    public void Modify_ChangesOnlyTheEditedWord() =>
        AssertMerged("before\n\noriginal word\n\nafter\n", "before\n\nchanged word\n\nafter\n");

    [Fact]
    public void Reorder_MovesUniqueSectionsWithoutReformattingThem() =>
        AssertProjected("before\n\nfirst\n\nsecond\n\nafter\n", "before\n\nsecond\n\nfirst\n\nafter\n", "before\n\n\n\nsecond\n\nfirst\n\nafter\n");

    [Fact]
    public void DisjointEdits_ComposeExternalAndLocalChanges()
    {
        const string body = "local target\n\nexternal target\n";
        var result = ProjectedMarkdownMerge.Merge(body, body, "local change\n\nexternal target\n", "local target\n\nexternal change\n");

        Assert.Null(result.Conflict);
        Assert.Equal("local change\n\nexternal change\n", result.Body);
    }

    [Fact]
    public void Overlap_ProducesAConflictInsteadOfChoosingAWriter()
    {
        const string body = "before\n\nsame target\n\nafter\n";
        var result = ProjectedMarkdownMerge.Merge(body, body, "before\n\nlocal target\n\nafter\n", "before\n\nremote target\n\nafter\n");

        Assert.Null(result.Body);
        Assert.NotNull(result.Conflict);
    }

    [Fact]
    public void RepeatedSections_AmbiguousExternalEditIsRejected()
    {
        const string body = "repeat\n\nrepeat\n";
        var result = ProjectedMarkdownMerge.Merge(body, body, body, "remote repeat\n\nrepeat\n");

        Assert.Null(result.Body);
        Assert.NotNull(result.Conflict);
    }

    [Fact]
    public void HeadingStructure_MutationIsObservable() =>
        AssertMerged("before\n\n## original\n\nbody\n\nafter\n", "before\n\n### remote\n\nbody\n\nafter\n");

    [Fact]
    public void InlineCode_MutationIsObservable() =>
        AssertMerged("before\n\nuse `one` here\n\nafter\n", "before\n\nuse `two` here\n\nafter\n");

    [Fact]
    public void LinkText_MutationIsObservable() =>
        AssertMerged("before\n\n[one](https://example.test)\n\nafter\n", "before\n\n[two](https://example.test)\n\nafter\n");

    [Fact]
    public void NestedQuote_MutationPreservesOuterStructure() =>
        AssertMerged("before\n\n> outer\n>> inner\n\nafter\n", "before\n\n> outer\n>> remote inner\n\nafter\n");

    [Fact]
    public void MultiLineCode_MutationPreservesUnchangedLines() =>
        AssertMerged("before\n\n```text\nfirst\nsecond\nthird\n```\n\nafter\n", "before\n\n```text\nfirst\nremote\nthird\n```\n\nafter\n");

    [Fact]
    public void EscapedLinkTarget_MutationDoesNotNormalizeTheLabel() =>
        AssertMerged("before\n\n[keep\\!](https://one.example)\n\nafter\n", "before\n\n[keep\\!](https://two.example)\n\nafter\n");

    [Fact]
    public void Slice11Fixture_SemanticPunctuationMutationLeavesAllOtherBytesExact()
    {
        var fixture = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "slice-11-sanitized.md"));
        var changed = fixture.Replace("watchdog fixture.", "watchdog fixture!", StringComparison.Ordinal);

        AssertMerged(fixture, changed);
    }

    [Fact]
    public void MixedLineEndings_ComposedInsertion_PreservesProjectedBytes()
    {
        var path = Path.Combine(Path.GetTempPath(), "dydo-fidelity-" + Guid.NewGuid().ToString("N") + ".md");
        const string body = "one\r\n\r\ntwo";
        try
        {
            File.WriteAllText(path, "---\r\ntitle: Note\r\n---\r\n\r\n" + body);
            var current = SyncDocFile.Read(path, "note", path);
            var projected = ProjectedMarkdownMerge.Merge(body, body, body, "inserted\n\none\r\n\r\ntwo");
            Assert.Null(projected.Conflict);
            var desired = new SyncDoc { LocalId = "note", SourcePath = path, Fields = current.Fields,
                Body = projected.Body! };

            SyncDocFile.PatchExisting(path, current, desired, patchFields: false, patchBody: true);

            Assert.Equal("---\r\ntitle: Note\r\n---\r\n\r\ninserted\n\none\r\n\r\ntwo", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void MixedLineEndings_ComposedDeletion_PreservesUntouchedTerminators()
    {
        var path = Path.Combine(Path.GetTempPath(), "dydo-fidelity-" + Guid.NewGuid().ToString("N") + ".md");
        const string body = "one\r\n\r\nremove\n\ntwo\r\n";
        try
        {
            File.WriteAllText(path, "---\r\ntitle: Note\n---\r\n\r\n" + body);
            var current = SyncDocFile.Read(path, "note", path);
            var projected = ProjectedMarkdownMerge.Merge(body, body, body,
                "one\r\n\r\ntwo\r\n");
            Assert.Null(projected.Conflict);
            var desired = new SyncDoc { LocalId = "note", SourcePath = path, Fields = current.Fields, Body = projected.Body! };

            SyncDocFile.PatchExisting(path, current, desired, patchFields: false, patchBody: true);

            Assert.Equal("---\r\ntitle: Note\n---\r\n\r\none\r\n\r\n\n\ntwo\r\n", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }

    private static void AssertMerged(string localBase, string currentExternal) =>
        AssertProjected(localBase, localBase, localBase, currentExternal, currentExternal, null);

    private static void AssertAsymmetric(string local, string external, string? title)
    {
        var result = ProjectedMarkdownMerge.Merge(local, external, local, external, title);
        Assert.Null(result.Conflict);
        Assert.Equal(local, result.Body);
    }

    private static void AssertProjected(string localBase, string currentExternal, string expected) =>
        AssertProjected(localBase, localBase, localBase, currentExternal, expected, null);

    private static void AssertProjected(string localBase, string externalBase, string currentLocal, string currentExternal,
        string expected, string? pageTitle)
    {
        var result = ProjectedMarkdownMerge.Merge(localBase, externalBase, currentLocal, currentExternal, pageTitle);

        Assert.Null(result.Conflict);
        Assert.Equal(expected, result.Body);
        Assert.NotEqual(localBase, result.Body);
        var changed = FirstDifference(localBase, result.Body!);
        Assert.Equal(localBase[..changed], result.Body![..changed]);
        Assert.Equal(localBase[(localBase.Length - CommonSuffix(localBase, result.Body!))..],
            result.Body![(result.Body!.Length - CommonSuffix(localBase, result.Body!))..]);
    }

    private static int FirstDifference(string left, string right)
    {
        var end = Math.Min(left.Length, right.Length);
        for (var index = 0; index < end; index++)
            if (left[index] != right[index])
                return index;
        return end;
    }

    private static int CommonSuffix(string left, string right)
    {
        var count = 0;
        while (count < left.Length && count < right.Length && left[^(count + 1)] == right[^(count + 1)])
            count++;
        return count;
    }
}
