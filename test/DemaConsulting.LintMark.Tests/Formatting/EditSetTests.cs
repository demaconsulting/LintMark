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
    ///     Test that an edit that contains an earlier edit is rejected.
    /// </summary>
    [Fact]
    public void EditSet_Add_EditContainingExistingEdit_Throws()
    {
        // Arrange: a set with an edit of characters 4..6
        var set = new EditSet();
        set.Add(Edit(4, 2));

        // Act / Assert: an edit of characters 0..10 contains it and is rejected
        Assert.Throws<InvalidOperationException>(() => set.Add(Edit(0, 10)));
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
    ///     Test that an insertion strictly inside a replaced range is rejected.
    /// </summary>
    [Fact]
    public void EditSet_Add_InsertionStrictlyInsideReplacement_Throws()
    {
        // Arrange: a set with a replacement of characters 5..8
        var set = new EditSet();
        set.Add(Edit(5, 3));

        // Act / Assert: an insertion at offset 6 is inside the replaced range
        Assert.Throws<InvalidOperationException>(() => set.Add(Edit(6, 0)));
        Assert.Equal(1, set.Count);
    }

    /// <summary>
    ///     Test that a replacement added after an insertion strictly inside its range is rejected.
    /// </summary>
    [Fact]
    public void EditSet_Add_ReplacementAroundExistingInsertion_Throws()
    {
        // Arrange: a set with an insertion at offset 6
        var set = new EditSet();
        set.Add(Edit(6, 0));

        // Act / Assert: a replacement of characters 5..8 would swallow the insertion
        Assert.Throws<InvalidOperationException>(() => set.Add(Edit(5, 3)));
        Assert.Equal(1, set.Count);
    }

    /// <summary>
    ///     Test that two edits of the same range are rejected.
    /// </summary>
    [Fact]
    public void EditSet_Add_IdenticalRange_Throws()
    {
        // Arrange: a set with an edit of characters 5..8
        var set = new EditSet();
        set.Add(Edit(5, 3, "a"));

        // Act / Assert: another edit of the same characters is rejected
        Assert.Throws<InvalidOperationException>(() => set.Add(Edit(5, 3, "b")));
        Assert.Equal(1, set.Count);
        Assert.Equal("a", Assert.Single(set.ToImmutable()).Replacement);
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
    ///     Test that a set without edits yields an initialized, empty snapshot.
    /// </summary>
    [Fact]
    public void EditSet_ToImmutable_NoEdits_ReturnsEmpty()
    {
        // Arrange: an empty set
        var set = new EditSet();

        // Act: take a snapshot
        var edits = set.ToImmutable();

        // Assert: the snapshot is empty but usable
        Assert.False(edits.IsDefault);
        Assert.True(edits.IsEmpty);
    }

    /// <summary>
    ///     Test that a snapshot cannot be modified through a cast to a mutable interface.
    /// </summary>
    [Fact]
    public void EditSet_ToImmutable_CastToMutableTypes_CannotBeModified()
    {
        // Arrange: a set with two edits
        var set = new EditSet();
        set.Add(Edit(0, 1));
        set.Add(Edit(4, 1));

        // Act: take a snapshot
        var edits = set.ToImmutable();

        // Assert: neither the read-only views nor the mutable interfaces allow a change
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyList<TextEdit>)edits);
        ReadOnlyAssertions.AssertCannotBeModified((IReadOnlyCollection<TextEdit>)edits);
        var mutable = (IList<TextEdit>)edits;
        Assert.True(mutable.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutable.Add(Edit(9, 1)));
        Assert.Throws<NotSupportedException>(() => mutable[0] = Edit(2, 1));
        Assert.Equal(2, edits.Length);
    }

    /// <summary>
    ///     Test that adding edits after a snapshot was taken does not change the snapshot.
    /// </summary>
    [Fact]
    public void EditSet_ToImmutable_LaterAdds_DoNotChangeEarlierSnapshot()
    {
        // Arrange: a set with one edit and a snapshot of it
        var set = new EditSet();
        set.Add(Edit(0, 1));
        var snapshot = set.ToImmutable();

        // Act: add another edit and take a second snapshot
        set.Add(Edit(5, 1));
        var later = set.ToImmutable();

        // Assert: the first snapshot is unchanged
        Assert.Single(snapshot);
        Assert.Equal(2, later.Length);
    }

    /// <summary>
    ///     Test that the count follows the number of added edits.
    /// </summary>
    [Fact]
    public void EditSet_Count_AfterAdds_CountsEdits()
    {
        // Arrange: an empty set
        var set = new EditSet();
        Assert.Equal(0, set.Count);

        // Act: add two edits
        set.Add(Edit(0, 1));
        set.Add(Edit(3, 0));

        // Assert: both are counted and the set is not empty
        Assert.Equal(2, set.Count);
        Assert.False(set.IsEmpty);
    }

    /// <summary>
    ///     Test that a new set is empty.
    /// </summary>
    [Fact]
    public void EditSet_IsEmpty_NewSet_IsTrue()
    {
        // Arrange / Act: a new set
        var set = new EditSet();

        // Assert: it holds no edit
        Assert.True(set.IsEmpty);
        Assert.Equal(0, set.Count);
    }

    /// <summary>
    ///     Test that the end offset is the start plus the length.
    /// </summary>
    [Fact]
    public void TextEdit_End_AnyEdit_ReturnsStartPlusLength()
    {
        // Arrange: an edit of characters 3..7 and an insertion at 9
        var replacement = new TextEdit(3, 4, "x", Rule);
        var insertion = new TextEdit(9, 0, "x", Rule);

        // Act / Assert: the end of a replacement is exclusive, the end of an insertion is its start
        Assert.Equal(7, replacement.End);
        Assert.Equal(9, insertion.End);
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
    ///     Gets the next value of a small deterministic pseudo-random sequence (a bit-shift generator).
    /// </summary>
    /// <param name="state">The state of the sequence; updated.</param>
    /// <param name="bound">The exclusive upper bound of the value.</param>
    /// <returns>A value from zero up to but excluding <paramref name="bound"/>.</returns>
    private static int Next(ref uint state, int bound)
    {
        state ^= state << 13;
        state ^= state >> 17;
        state ^= state << 5;
        return (int)(state % (uint)bound);
    }

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
    ///     Test that without edits the very same text instance is returned.
    /// </summary>
    [Fact]
    public void SourcePatcher_Apply_NoEdits_ReturnsSameText()
    {
        // Arrange: a document and no edits
        var source = Source("unchanged\n");

        // Act: patch
        var result = SourcePatcher.Apply(source, []);

        // Assert: the text instance is returned without a copy
        Assert.Same(source.Text, result);
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
    ///     Test that an insertion beyond the end of the text is rejected.
    /// </summary>
    [Fact]
    public void SourcePatcher_Apply_InsertionBeyondEnd_Throws()
    {
        // Arrange: a text of three characters and an insertion at offset 4
        var source = Source("abc");
        ImmutableArray<TextEdit> edits = [Edit(4, 0, "x")];

        // Act / Assert: offset 4 does not exist, offset 3 (the end) would
        Assert.Throws<ArgumentException>(() => SourcePatcher.Apply(source, edits));
    }

    /// <summary>
    ///     Test that edits that are not ordered by start offset are rejected.
    /// </summary>
    [Fact]
    public void SourcePatcher_Apply_UnorderedArray_Throws()
    {
        // Arrange: two edits in descending order
        var source = Source("abcdefgh");
        ImmutableArray<TextEdit> edits = [Edit(5, 1, "x"), Edit(0, 1, "y")];

        // Act / Assert: the patcher does not sort, it refuses
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
    ///     Test that an insertion strictly inside a replaced range is rejected.
    /// </summary>
    [Fact]
    public void SourcePatcher_Apply_InsertionInsideReplacement_Throws()
    {
        // Arrange: a replacement of characters 0..5 and an insertion at offset 2, inside it
        var source = Source("abcdefgh");
        ImmutableArray<TextEdit> edits = [Edit(0, 5, "x"), Edit(2, 0, "y")];

        // Act / Assert: the insertion conflicts with the replacement
        Assert.Throws<ArgumentException>(() => SourcePatcher.Apply(source, edits));
    }

    /// <summary>
    ///     Test that a replacement followed by an insertion at its start offset is rejected.
    /// </summary>
    [Fact]
    public void SourcePatcher_Apply_ReplacementThenInsertionAtSameStart_Throws()
    {
        // Arrange: a replacement of characters 2..5 followed by an insertion at offset 2
        var source = Source("abcdefgh");
        ImmutableArray<TextEdit> edits = [Edit(2, 3, "x"), Edit(2, 0, "y")];

        // Act / Assert: the order is the wrong way round, an insertion must come first
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
    ///     Test that an out-of-range edit that is not the first edit is rejected.
    /// </summary>
    [Fact]
    public void SourcePatcher_Apply_SecondEditBeyondText_Throws()
    {
        // Arrange: a valid first edit and a second edit of characters 2..12 in a text of three characters
        var source = Source("abc");
        ImmutableArray<TextEdit> edits = [Edit(0, 1, "x"), Edit(2, 10, "y")];

        // Act / Assert: every edit is checked, not only the first
        Assert.Throws<ArgumentException>(() => SourcePatcher.Apply(source, edits));
    }

    /// <summary>
    ///     Test that many random non-conflicting edits give the same result as a naive back-to-front application.
    /// </summary>
    [Fact]
    public void SourcePatcher_Apply_ManyEdits_MatchesReferenceImplementation()
    {
        // Arrange: a fixed seed, so a failure can always be reproduced
        var state = 2463534242u;
        for (var round = 0; round < 40; round++)
        {
            var text = RandomText(ref state);
            var edits = RandomEdits(ref state, text.Length);

            // Act: add the edits to a set in a shuffled order, then patch
            var set = new EditSet();
            foreach (var edit in Shuffle(ref state, edits))
            {
                set.Add(edit);
            }

            var actual = SourcePatcher.Apply(Source(text), set.ToImmutable());

            // Assert: the one-pass result equals applying the edits one by one from the end of the text
            Assert.Equal(ApplyBackwards(text, edits), actual);
        }
    }

    /// <summary>
    ///     Builds a random text.
    /// </summary>
    /// <param name="state">The state of the pseudo-random sequence; updated.</param>
    /// <returns>A text of 50 to 449 characters from a small alphabet that includes line feeds.</returns>
    private static string RandomText(ref uint state)
    {
        const string alphabet = "abc \n.#*";
        var characters = new char[50 + Next(ref state, 400)];
        for (var i = 0; i < characters.Length; i++)
        {
            characters[i] = alphabet[Next(ref state, alphabet.Length)];
        }

        return new string(characters);
    }

    /// <summary>
    ///     Builds random edits that do not conflict, in ascending order.
    /// </summary>
    /// <param name="state">The state of the pseudo-random sequence; updated.</param>
    /// <param name="textLength">The length of the text the edits apply to.</param>
    /// <returns>The edits: replacements, deletions and insertions, some of which touch.</returns>
    private static List<TextEdit> RandomEdits(ref uint state, int textLength)
    {
        var edits = new List<TextEdit>();
        var position = Next(ref state, 4);
        while (position <= textLength)
        {
            var editLength = Math.Min(Next(ref state, 5), textLength - position);
            edits.Add(Edit(position, editLength, new string('R', Next(ref state, 4))));

            // After an insertion at least one character must pass, otherwise two insertions would share an offset
            var minimumGap = editLength == 0 ? 1 : 0;
            position += editLength + minimumGap + Next(ref state, 6);
        }

        return edits;
    }

    /// <summary>
    ///     Puts edits in a random order.
    /// </summary>
    /// <param name="state">The state of the pseudo-random sequence; updated.</param>
    /// <param name="edits">The edits.</param>
    /// <returns>A shuffled copy.</returns>
    private static List<TextEdit> Shuffle(ref uint state, List<TextEdit> edits)
    {
        var shuffled = new List<TextEdit>(edits);
        for (var i = shuffled.Count - 1; i > 0; i--)
        {
            var j = Next(ref state, i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled;
    }

    /// <summary>
    ///     Applies ascending, non-conflicting edits one by one from the end of the text, the naive reference.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="edits">The edits in ascending order.</param>
    /// <returns>The patched text.</returns>
    private static string ApplyBackwards(string text, List<TextEdit> edits)
    {
        var result = new StringBuilder(text);
        for (var i = edits.Count - 1; i >= 0; i--)
        {
            result.Remove(edits[i].Start, edits[i].Length).Insert(edits[i].Start, edits[i].Replacement);
        }

        return result.ToString();
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
