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
    ///     Test that an edit that was not created with its constructor is rejected.
    /// </summary>
    [Fact]
    public void EditSet_Add_DefaultEdit_Throws()
    {
        // Arrange: an empty set
        var set = new EditSet();

        // Act / Assert: a default instance has no replacement and no rule
        Assert.Throws<ArgumentException>(() => set.Add(default));
        Assert.True(set.IsEmpty);
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
    ///     Test that the constructor stores every argument.
    /// </summary>
    [Fact]
    public void TextEdit_Constructor_AllArguments_ExposesValues()
    {
        // Arrange / Act: create an edit
        var edit = new TextEdit(3, 4, "new", "paragraph-wrap");

        // Assert: every value is exposed
        Assert.Equal(3, edit.Start);
        Assert.Equal(4, edit.Length);
        Assert.Equal("new", edit.Replacement);
        Assert.Equal("paragraph-wrap", edit.RuleId);
    }

    /// <summary>
    ///     Test that a negative start offset is rejected.
    /// </summary>
    [Fact]
    public void TextEdit_Constructor_NegativeStart_Throws()
    {
        // Arrange / Act / Assert: a negative start is out of range
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextEdit(-1, 0, "x", Rule));
    }

    /// <summary>
    ///     Test that a negative length is rejected.
    /// </summary>
    [Fact]
    public void TextEdit_Constructor_NegativeLength_Throws()
    {
        // Arrange / Act / Assert: a negative length is out of range
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextEdit(0, -1, "x", Rule));
    }

    /// <summary>
    ///     Test that a null replacement is rejected.
    /// </summary>
    [Fact]
    public void TextEdit_Constructor_NullReplacement_Throws()
    {
        // Arrange / Act / Assert: the replacement must not be null
        Assert.Throws<ArgumentNullException>(() => new TextEdit(0, 0, null!, Rule));
    }

    /// <summary>
    ///     Test that a null rule identifier is rejected.
    /// </summary>
    [Fact]
    public void TextEdit_Constructor_NullRuleId_Throws()
    {
        // Arrange / Act / Assert: the rule identifier must not be null
        Assert.Throws<ArgumentNullException>(() => new TextEdit(0, 0, "x", null!));
    }

    /// <summary>
    ///     Test that an empty rule identifier is rejected.
    /// </summary>
    [Fact]
    public void TextEdit_Constructor_EmptyRuleId_Throws()
    {
        // Arrange / Act / Assert: the rule identifier must not be empty
        Assert.Throws<ArgumentException>(() => new TextEdit(0, 0, "x", string.Empty));
    }

    /// <summary>
    ///     Test that a start and length whose sum overflows are rejected.
    /// </summary>
    [Fact]
    public void TextEdit_Constructor_StartPlusLengthOverflow_Throws()
    {
        // Arrange / Act / Assert: the end offset cannot be represented
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextEdit(int.MaxValue, 1, "x", Rule));
        Assert.Throws<ArgumentOutOfRangeException>(() => new TextEdit(1, int.MaxValue, "x", Rule));
    }

    /// <summary>
    ///     Test that the largest representable range is accepted.
    /// </summary>
    [Fact]
    public void TextEdit_Constructor_RangeEndingAtMaxValue_Accepted()
    {
        // Arrange / Act: an edit whose end is exactly int.MaxValue
        var edit = new TextEdit(1, int.MaxValue - 1, "x", Rule);

        // Assert: the end is representable
        Assert.Equal(int.MaxValue, edit.End);
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
    ///     Test that edits with the same values are equal and edits with different values are not.
    /// </summary>
    [Fact]
    public void TextEdit_Equals_SameValues_AreEqual()
    {
        // Arrange: two edits with the same values and one with another rule
        var first = new TextEdit(1, 2, "x", "paragraph-wrap");
        var second = new TextEdit(1, 2, "x", "paragraph-wrap");
        var other = new TextEdit(1, 2, "x", "heading-style");

        // Act / Assert: value equality covers every member
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, other);
    }
}
