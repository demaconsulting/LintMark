// Copyright (c) DEMA Consulting
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Collections.Immutable;
using System.Text;
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the EditSet class rejects conflicting edits, orders its edits deterministically and hands out
///     immutable snapshots, and that the TextEdit value type validates its arguments.
/// </summary>
public class EditSetTests
{
    /// <summary>
    ///     The rule identifier used by the edits of these tests.
    /// </summary>
    private const string Rule = "paragraph-wrap";

    /// <summary>
    ///     Creates an edit.
    /// </summary>
    /// <param name="start">The start offset.</param>
    /// <param name="length">The number of replaced characters.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <returns>The edit.</returns>
    private static TextEdit Edit(int start, int length, string replacement = "x") => new(start, length, replacement, Rule);

    /// <summary>
    ///     Gets every permutation of a list.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="items">The items.</param>
    /// <returns>All orderings of the items.</returns>
    private static List<List<T>> Permutations<T>(IReadOnlyList<T> items)
    {
        if (items.Count <= 1)
        {
            return [[.. items]];
        }

        var result = new List<List<T>>();
        for (var i = 0; i < items.Count; i++)
        {
            var rest = items.Where((_, index) => index != i).ToList();
            foreach (var tail in Permutations(rest))
            {
                tail.Insert(0, items[i]);
                result.Add(tail);
            }
        }

        return result;
    }

    /// <summary>
    ///     Test that an edit that shares characters with an earlier edit is rejected.
    /// </summary>
    [Fact]
    public void EditSet_Add_OverlappingEdit_Throws()
    {
        // Arrange: a set with an edit of characters 0..5
        var set = new EditSet();
        set.Add(Edit(0, 5));

        // Act / Assert: an edit of characters 3..8 overlaps and is rejected, and the message names the rules and ranges
        var exception = Assert.Throws<InvalidOperationException>(() => set.Add(Edit(3, 5)));
        Assert.Equal(
            "Edit of rule 'paragraph-wrap' at 3..8 conflicts with edit of rule 'paragraph-wrap' at 0..5.",
            exception.Message);
        Assert.Equal(1, set.Count);
    }

    /// <summary>
    ///     Test that an edit inside the range of an earlier edit is rejected.
    /// </summary>
    [Fact]
    public void EditSet_Add_ContainedEdit_Throws()
    {
        // Arrange: a set with an edit of characters 0..10
        var set = new EditSet();
        set.Add(Edit(0, 10));

        // Act / Assert: an edit of characters 2..5 lies inside it and is rejected
        Assert.Throws<InvalidOperationException>(() => set.Add(Edit(2, 3)));
        Assert.Equal(1, set.Count);
    }

    /// <summary>
    ///     Test that edits that merely touch are accepted.
    /// </summary>
    [Fact]
    public void EditSet_Add_AdjacentEdits_Accepted()
    {
        // Arrange: a set with an edit of characters 0..3
        var set = new EditSet();
        set.Add(Edit(0, 3));

        // Act: add an edit that starts where the first ends and two edits that touch each other
        set.Add(Edit(3, 3));
        set.Add(Edit(10, 2));
        set.Add(Edit(12, 2));

        // Assert: all edits are in the set
        Assert.Equal(4, set.Count);
    }

    /// <summary>
    ///     Test that two insertions at one offset are rejected because their order would be arbitrary.
    /// </summary>
    [Fact]
    public void EditSet_Add_TwoInsertionsAtSameOffset_Throws()
    {
        // Arrange: a set with an insertion at offset 5
        var set = new EditSet();
        set.Add(Edit(5, 0, "a"));

        // Act / Assert: a second insertion at offset 5 is rejected
        Assert.Throws<InvalidOperationException>(() => set.Add(Edit(5, 0, "b")));
        Assert.Equal(1, set.Count);
    }

    /// <summary>
    ///     Test that an insertion sorts before a replacement that starts at the same offset.
    /// </summary>
    [Fact]
    public void EditSet_Add_InsertionBeforeReplacementAtSameStart_OrdersInsertionFirst()
    {
        // Arrange: a set with a replacement of characters 5..8
        var set = new EditSet();
        set.Add(Edit(5, 3, "r"));

        // Act: add an insertion at the start of the replacement
        set.Add(Edit(5, 0, "i"));

        // Assert: the insertion comes first
        var edits = set.ToImmutable();
        Assert.Equal(2, edits.Length);
        Assert.Equal(0, edits[0].Length);
        Assert.Equal(3, edits[1].Length);
    }

    /// <summary>
    ///     Test that an insertion at the end of a replaced range is accepted.
    /// </summary>
    [Fact]
    public void EditSet_Add_InsertionAtEndOfReplacement_Accepted()
    {
        // Arrange: a set with a replacement of characters 5..8
        var set = new EditSet();
        set.Add(Edit(5, 3));

        // Act: insert at offset 8
        set.Add(Edit(8, 0));

        // Assert: both edits are in the set, the replacement first
        var edits = set.ToImmutable();
        Assert.Equal(2, edits.Length);
        Assert.Equal(5, edits[0].Start);
        Assert.Equal(8, edits[1].Start);
    }

    /// <summary>
    ///     Test that the snapshot is the same for every order in which the edits were added.
    /// </summary>
    [Fact]
    public void EditSet_ToImmutable_AnyAddOrder_SameOrderedResult()
    {
        // Arrange: five edits that do not conflict, including an insertion that touches two replacements
        var ordered = new List<TextEdit> { Edit(0, 2), Edit(2, 0), Edit(2, 3), Edit(7, 0), Edit(9, 1) };

        // Act: add them in every possible order
        var snapshots = Permutations(ordered).Select(order =>
        {
            var set = new EditSet();
            foreach (var edit in order)
            {
                set.Add(edit);
            }

            return set.ToImmutable().ToArray();
        }).ToList();

        // Assert: every snapshot equals the edits ordered by start offset and then length
        Assert.Equal(120, snapshots.Count);
        Assert.All(snapshots, snapshot => Assert.Equal(ordered, snapshot));
    }

    /// <summary>
    ///     Test that an edit that starts at an offset is found, and only when it is the one that the predicate accepts.
    /// </summary>
    [Fact]
    public void EditSet_FindStartingAt_Offset_FindsTheAcceptedEdit()
    {
        // Arrange: an insertion and a replacement at one offset, and an edit elsewhere
        var set = new EditSet();
        set.Add(new TextEdit(2, 0, "ab", "a"));
        set.Add(new TextEdit(2, 1, "x", "b"));
        set.Add(new TextEdit(7, 1, "y", "c"));

        // Act & Assert: the predicate chooses between the two that start at 2; none starts at 3
        Assert.Equal("a", set.FindStartingAt(2, static edit => edit.Length == 0)?.RuleId);
        Assert.Equal("b", set.FindStartingAt(2, static edit => edit.Length == 1)?.RuleId);
        Assert.Equal("c", set.FindStartingAt(7, static _ => true)?.RuleId);
        Assert.Null(set.FindStartingAt(3, static _ => true));
        Assert.Null(set.FindStartingAt(2, static edit => edit.Length == 5));
    }
}

/// <summary>
///     Test that the SourcePatcher class applies ordered edits to the text of a document and rejects every
///     edit array that is not valid.
/// </summary>
public class SourcePatcherTests
{
    /// <summary>
    ///     The rule identifier used by the edits of these tests.
    /// </summary>
    private const string Rule = "paragraph-wrap";

    /// <summary>
    ///     Creates a source document.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <returns>The document.</returns>
    private static SourceDocument Source(string text) => SourceDocument.FromText("a.md", text);

    /// <summary>
    ///     Creates an edit.
    /// </summary>
    /// <param name="start">The start offset.</param>
    /// <param name="length">The number of replaced characters.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <returns>The edit.</returns>
    private static TextEdit Edit(int start, int length, string replacement) => new(start, length, replacement, Rule);

    /// <summary>
    ///     Test that replacement, deletion and insertion are applied together.
    /// </summary>
    [Fact]
    public void SourcePatcher_Apply_ReplaceDeleteInsert_ProducesExpectedText()
    {
        // Arrange: a text and one replacement, one deletion and one insertion
        var source = Source("hello world\n");
        var set = new EditSet();
        set.Add(Edit(0, 5, "HELLO"));
        set.Add(Edit(5, 1, string.Empty));
        set.Add(Edit(11, 0, "!"));

        // Act: patch
        var result = SourcePatcher.Apply(source, set.ToImmutable());

        // Assert: every edit is applied and the rest is untouched
        Assert.Equal("HELLOworld!\n", result);
    }

    /// <summary>
    ///     Test that an insertion and a replacement at the very end of the text are applied.
    /// </summary>
    [Fact]
    public void SourcePatcher_Apply_EditAtEndOfText_Applies()
    {
        // Arrange: a text with a replacement of its last character and an insertion after it
        var source = Source("abc");
        var set = new EditSet();
        set.Add(Edit(2, 1, "C"));
        set.Add(Edit(3, 0, "\n"));

        // Act: patch
        var result = SourcePatcher.Apply(source, set.ToImmutable());

        // Assert: both edits are applied
        Assert.Equal("abC\n", result);
    }

    /// <summary>
    ///     Test that an edit that reaches beyond the text is rejected.
    /// </summary>
    [Fact]
    public void SourcePatcher_Apply_EditBeyondText_Throws()
    {
        // Arrange: a text of three characters and an edit of characters 2..7
        var source = Source("abc");
        ImmutableArray<TextEdit> edits = [Edit(2, 5, "x")];

        // Act / Assert: the edit lies outside the text
        Assert.Throws<ArgumentException>(() => SourcePatcher.Apply(source, edits));
    }

    /// <summary>
    ///     Test that overlapping edits are rejected.
    /// </summary>
    [Fact]
    public void SourcePatcher_Apply_OverlappingEdits_Throws()
    {
        // Arrange: two edits that share characters
        var source = Source("abcdefgh");
        ImmutableArray<TextEdit> edits = [Edit(0, 5, "x"), Edit(3, 2, "y")];

        // Act / Assert: the overlap is refused
        Assert.Throws<ArgumentException>(() => SourcePatcher.Apply(source, edits));
    }

    /// <summary>
    ///     Test that an insertion followed by a replacement at the same start offset is applied.
    /// </summary>
    [Fact]
    public void SourcePatcher_Apply_InsertionThenReplacementAtSameStart_Applies()
    {
        // Arrange: an insertion at offset 2 followed by a replacement of characters 2..5, built by hand
        var source = Source("abcdefgh");
        ImmutableArray<TextEdit> edits = [Edit(2, 0, "1"), Edit(2, 3, "2")];

        // Act: patch
        var result = SourcePatcher.Apply(source, edits);

        // Assert: the inserted text comes first and the replacement follows it
        Assert.Equal("ab12fgh", result);
    }

    /// <summary>
    ///     Test that offsets are UTF-16 code unit offsets, so text with surrogate pairs is patched correctly.
    /// </summary>
    [Fact]
    public void SourcePatcher_Apply_UnicodeText_PatchesByUtf16Offsets()
    {
        // Arrange: a text with an emoji (two UTF-16 units) and an accented letter, and edits that surround the emoji
        var source = Source("\u00e9\U0001F600b\n");
        var set = new EditSet();
        set.Add(Edit(0, 1, "e"));
        set.Add(Edit(1, 2, "<smile>"));
        set.Add(Edit(3, 1, "B"));

        // Act: patch
        var result = SourcePatcher.Apply(source, set.ToImmutable());

        // Assert: the surrogate pair is replaced as one unit and its neighbors are patched correctly
        Assert.Equal("e<smile>B\n", result);
    }
}
