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
using DemaConsulting.LintMark.Document;
using DemaConsulting.LintMark.Formatting;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Test that the EditRebaser class composes edits that were computed on the output of other edits into edits on the
///     original text, and maps offsets of the output back to the original.
/// </summary>
public class EditRebaserTests
{
    /// <summary>
    ///     Creates an edit of the rule used by these tests.
    /// </summary>
    /// <param name="start">Offset of the first replaced character.</param>
    /// <param name="length">Number of replaced characters.</param>
    /// <param name="replacement">The new text.</param>
    /// <returns>The edit.</returns>
    private static TextEdit Edit(int start, int length, string replacement) => new(start, length, replacement, "r");

    /// <summary>
    ///     Applies edits to a text.
    /// </summary>
    /// <param name="text">The LF-normalized text.</param>
    /// <param name="edits">The edits, ordered and without conflicts.</param>
    /// <returns>The patched text.</returns>
    private static string Apply(string text, ImmutableArray<TextEdit> edits) =>
        SourcePatcher.Apply(SourceDocument.FromText("a.md", text), edits);

    /// <summary>
    ///     Test that edits against the same text need no translation when one list is empty.
    /// </summary>
    [Fact]
    public void EditRebaser_Rebase_OneListEmpty_ReturnsTheOtherUnchanged()
    {
        // Arrange: one list of edits
        ImmutableArray<TextEdit> edits = [Edit(1, 1, "x"), Edit(4, 0, "y")];

        // Act & Assert: the other list is returned as it is, whichever side is empty
        Assert.Equal(edits, EditRebaser.Rebase(edits, []));
        Assert.Equal(edits, EditRebaser.Rebase([], edits));
        Assert.Empty(EditRebaser.Rebase([], []));
    }

    /// <summary>
    ///     Test that an edit that replaces nothing with nothing is dropped and does not get in the way of an insertion at its place.
    /// </summary>
    [Fact]
    public void EditRebaser_Rebase_EditThatChangesNothing_IsDropped() =>
        Assert.Equal([Edit(2, 0, "x")], EditRebaser.Rebase([Edit(2, 0, string.Empty)], [Edit(2, 0, "x")]));

    /// <summary>
    ///     Test that edits that lie outside each other are translated by the change in length of the edits before them.
    /// </summary>
    [Fact]
    public void EditRebaser_Rebase_EditsOutsideEachOther_AreTranslated()
    {
        // Arrange: the first edit lengthens the text by two, the second replaces a character after it
        const string original = "abcdef";
        ImmutableArray<TextEdit> first = [Edit(0, 1, "AAA")];
        ImmutableArray<TextEdit> second = [Edit(5, 1, "D")];

        // Act: compose
        var composed = EditRebaser.Rebase(first, second);

        // Assert: both stay, the second at its place in the original, and the result equals applying them one after the other
        Assert.Equal([Edit(0, 1, "AAA"), Edit(3, 1, "D")], composed);
        Assert.Equal(Apply(Apply(original, first), second), Apply(original, composed));
    }

    /// <summary>
    ///     Test that an edit of the first list whose output lies inside the range the second list replaces is absorbed.
    /// </summary>
    [Fact]
    public void EditRebaser_Rebase_FirstEditInsideReplacedRange_IsAbsorbed()
    {
        // Arrange: the first edit changes one character, the second replaces a range that holds its output
        const string original = "abcdef";
        ImmutableArray<TextEdit> first = [Edit(2, 1, "XY")];
        ImmutableArray<TextEdit> second = [Edit(1, 5, "Z")];

        // Act: compose
        var composed = EditRebaser.Rebase(first, second);

        // Assert: one edit over the original range, and the same text as in two steps
        Assert.Equal([Edit(1, 4, "Z")], composed);
        Assert.Equal(Apply(Apply(original, first), second), Apply(original, composed));
    }

    /// <summary>
    ///     Test that an edit of the second list that starts or ends inside the output of the first is extended to all of that output.
    /// </summary>
    [Fact]
    public void EditRebaser_Rebase_SecondEditInsideFirstOutput_IsExtendedToItsEnds()
    {
        // Arrange: the second edit changes the middle of the text that the first one wrote
        const string original = "abcdef";
        ImmutableArray<TextEdit> first = [Edit(2, 1, "XYZ")];
        ImmutableArray<TextEdit> second = [Edit(3, 1, "q")];

        // Act: compose
        var composed = EditRebaser.Rebase(first, second);

        // Assert: one edit that replaces the original character with the changed text
        Assert.Equal([Edit(2, 1, "XqZ")], composed);
        Assert.Equal(Apply(Apply(original, first), second), Apply(original, composed));
    }

    /// <summary>
    ///     Test that a deletion of the first list that only touches the start of the range the second list replaces stays.
    /// </summary>
    [Fact]
    public void EditRebaser_Rebase_DeletionTouchingTheReplacedRange_StaysOutside()
    {
        // Arrange: the first list deletes a character, the second replaces the character that then stands at that place
        const string original = "abcdef";
        ImmutableArray<TextEdit> first = [Edit(2, 1, string.Empty)];
        ImmutableArray<TextEdit> second = [Edit(2, 1, "Q")];

        // Act: compose
        var composed = EditRebaser.Rebase(first, second);

        // Assert: both edits are kept and the text is as in two steps
        Assert.Equal([Edit(2, 1, string.Empty), Edit(3, 1, "Q")], composed);
        Assert.Equal(Apply(Apply(original, first), second), Apply(original, composed));
    }

    /// <summary>
    ///     Test that an insertion of the second list that touches or lies inside an insertion of the first is merged with it.
    /// </summary>
    [Theory]
    [InlineData(2, "qXX")]
    [InlineData(3, "XqX")]
    [InlineData(4, "XXq")]
    public void EditRebaser_Rebase_InsertionTouchingAnInsertion_IsMergedWithIt(int position, string expected)
    {
        // Arrange: the first list inserts two characters, the second inserts one at, inside or after them
        const string original = "abcdef";
        ImmutableArray<TextEdit> first = [Edit(2, 0, "XX")];
        ImmutableArray<TextEdit> second = [Edit(position, 0, "q")];

        // Act: compose
        var composed = EditRebaser.Rebase(first, second);

        // Assert: one insertion with the combined text
        Assert.Equal([Edit(2, 0, expected)], composed);
        Assert.Equal(Apply(Apply(original, first), second), Apply(original, composed));
    }

    /// <summary>
    ///     Test that two edits of the second list that fall in the output of one edit of the first are merged into one.
    /// </summary>
    [Fact]
    public void EditRebaser_Rebase_TwoSecondEditsInOneFirstOutput_AreMerged()
    {
        // Arrange: the first edit writes five characters, the second list changes the second and the fourth
        const string original = "abcdef";
        ImmutableArray<TextEdit> first = [Edit(2, 1, "12345")];
        ImmutableArray<TextEdit> second = [Edit(3, 1, "A"), Edit(5, 1, "B")];

        // Act: compose
        var composed = EditRebaser.Rebase(first, second);

        // Assert: one edit over the original character
        Assert.Equal([Edit(2, 1, "1A3B5")], composed);
        Assert.Equal(Apply(Apply(original, first), second), Apply(original, composed));
    }

    /// <summary>
    ///     Test that the composition of random edits equals applying the two lists one after the other.
    /// </summary>
    [Fact]
    public void EditRebaser_Rebase_RandomEdits_EqualApplyingTheListsInSequence()
    {
        // Arrange: a seeded generator of texts and edit lists
        var random = new Random(20260514);
        var failures = new List<string>();

        // Act: compose random lists and compare with the sequential result
        for (var round = 0; round < 6000; round++)
        {
            var original = RandomText(random, 0, 24);
            var first = RandomEdits(random, original.Length);
            var intermediate = Apply(original, first);
            var second = RandomEdits(random, intermediate.Length);
            try
            {
                var composed = EditRebaser.Rebase(first, second);
                var expected = Apply(intermediate, second);
                var actual = Apply(original, composed);
                if (!string.Equals(expected, actual, StringComparison.Ordinal))
                {
                    failures.Add($"{Describe(original, first, second)} -> expected '{expected}', got '{actual}'");
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                failures.Add($"{Describe(original, first, second)} -> {ex.GetType().Name}: {ex.Message}");
            }
        }

        // Assert: no round differs
        Assert.True(failures.Count == 0, $"{failures.Count} round(s) failed:\n{string.Join("\n", failures.Take(5))}");
    }

    /// <summary>
    ///     Test that the offsets of the output map back to the original text, and an offset inside written text maps to the start of the replaced range.
    /// </summary>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(6, 4)]
    [InlineData(8, 6)]
    public void EditRebaser_CreateOffsetMapper_OffsetsOfTheOutput_MapBackToTheOriginal(int offset, int expected)
    {
        // Arrange: one edit replaces the third character by three characters
        ImmutableArray<TextEdit> first = [Edit(2, 1, "XYZ")];

        // Act & Assert: the offsets map as expected
        Assert.Equal(expected, EditRebaser.CreateOffsetMapper(first)(offset));
    }

    /// <summary>
    ///     Test that the offset after a deletion maps behind the deleted characters.
    /// </summary>
    [Fact]
    public void EditRebaser_CreateOffsetMapper_AfterADeletion_MapsBehindTheDeletedCharacters() =>
        Assert.Equal(5, EditRebaser.CreateOffsetMapper([Edit(2, 3, string.Empty)])(2));

    /// <summary>
    ///     Test that composing and mapping very many edits takes time in proportion to their number, which a search through all
    ///     the edits for each one of them would not (a defect that made formatting a large document time out).
    /// </summary>
    [Fact]
    public void EditRebaser_Rebase_VeryManyEdits_TakesTimeInProportionToTheirNumber()
    {
        // Arrange: a first edit of every ten characters, and a second one inside the text each of them writes
        const int Count = 200_000;
        var replacements = ImmutableArray.CreateBuilder<TextEdit>(Count);
        var insertions = ImmutableArray.CreateBuilder<TextEdit>(Count);
        var insideReplacements = ImmutableArray.CreateBuilder<TextEdit>(Count);
        var afterInsertions = ImmutableArray.CreateBuilder<TextEdit>(Count);
        for (var i = 0; i < Count; i++)
        {
            replacements.Add(Edit(10 * i, 2, "xyz"));
            insideReplacements.Add(Edit((11 * i) + 1, 1, "Q"));
            insertions.Add(Edit(10 * i, 0, "ab"));
            afterInsertions.Add(Edit((12 * i) + 2, 0, "Z"));
        }

        // Act: compose replacements and insertions, and map every offset of the produced text back
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var replaced = EditRebaser.Rebase(replacements.MoveToImmutable(), insideReplacements.MoveToImmutable());
        var inserted = EditRebaser.Rebase(insertions.MoveToImmutable(), afterInsertions.MoveToImmutable());
        var map = EditRebaser.CreateOffsetMapper(replaced);
        var mapped = 0;
        for (var i = 0; i < Count; i++)
        {
            mapped += map(11 * i) - (10 * i);
        }

        watch.Stop();

        // Assert: every pair became one edit that writes both texts; a quadratic search would need minutes
        Assert.Equal(Count, replaced.Length);
        Assert.Equal(Edit(50, 2, "xQz"), replaced[5]);
        Assert.Equal(Count, inserted.Length);
        Assert.Equal(Edit(50, 0, "abZ"), inserted[5]);
        Assert.Equal(0, mapped);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(20), $"Composing and mapping {Count} edits took {watch.Elapsed}");
    }

    /// <summary>
    ///     Creates a random text from a small alphabet that includes the line feed.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <param name="minimum">The least length.</param>
    /// <param name="maximum">The greatest length.</param>
    /// <returns>The text.</returns>
    private static string RandomText(Random random, int minimum, int maximum)
    {
        const string alphabet = "abcde \n";
        return new string(Enumerable.Range(0, random.Next(minimum, maximum + 1)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
    }

    /// <summary>
    ///     Creates a random list of ordered edits without conflicts for a text.
    /// </summary>
    /// <param name="random">The generator.</param>
    /// <param name="length">The length of the text.</param>
    /// <returns>The edits.</returns>
    private static ImmutableArray<TextEdit> RandomEdits(Random random, int length)
    {
        var set = new EditSet();
        var attempts = random.Next(0, 7);
        for (var i = 0; i < attempts; i++)
        {
            var start = random.Next(0, length + 1);
            var span = random.Next(0, Math.Min(3, length - start) + 1);
            try
            {
                set.Add(Edit(start, span, RandomText(random, 0, 4)));
            }
            catch (InvalidOperationException)
            {
                // A conflicting candidate is not part of the list
            }
        }

        return set.ToImmutable();
    }

    /// <summary>
    ///     Describes a failing round for the message.
    /// </summary>
    /// <param name="original">The original text.</param>
    /// <param name="first">The first list.</param>
    /// <param name="second">The second list.</param>
    /// <returns>The description.</returns>
    private static string Describe(string original, ImmutableArray<TextEdit> first, ImmutableArray<TextEdit> second) =>
        $"text '{original.Replace("\n", "\\n", StringComparison.Ordinal)}' A=[{string.Join(';', first.Select(Show))}] B=[{string.Join(';', second.Select(Show))}]";

    /// <summary>
    ///     Test that a rebaser that does not absorb the edits of the other rules would be caught: the composition of edits in
    ///     which an edit lies inside a rewritten paragraph differs from applying the lists one after the other.
    /// </summary>
    [Fact]
    public void EditRebaser_Rebase_EditInsideRewrittenParagraph_EqualsApplyingInSequence()
    {
        // Arrange: an edit of another rule inside the text that a wrap rewrites
        const string original = "ant _bbb_ cat\n";
        ImmutableArray<TextEdit> first = [new TextEdit(4, 1, "*", "emphasis"), new TextEdit(8, 1, "*", "emphasis")];
        ImmutableArray<TextEdit> second = [new TextEdit(0, 14, "ant *bat*\ncat\n", "line-length")];

        // Act: the correct composition
        var source = SourceDocument.FromText("a.md", original);
        var intermediate = SourcePatcher.Apply(source, first);
        var expected = SourcePatcher.Apply(SourceDocument.FromText("a.md", intermediate), second);
        var composed = SourcePatcher.Apply(source, EditRebaser.Rebase(first, second));

        // Assert: the composition equals the sequence, and the faulty one that keeps both lists cannot be applied (the edits conflict)
        Assert.Equal(expected, composed);
        Assert.Throws<InvalidOperationException>(() =>
        {
            var set = new EditSet();
            foreach (var edit in first.Concat(second))
            {
                set.Add(edit);
            }
        });
    }

    /// <summary>
    ///     Shows an edit for the message.
    /// </summary>
    /// <param name="edit">The edit.</param>
    /// <returns>The text.</returns>
    private static string Show(TextEdit edit) =>
        $"{edit.Start}+{edit.Length}='{edit.Replacement.Replace("\n", "\\n", StringComparison.Ordinal)}'";
}
