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

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Assertions that a collection exposed through a read-only interface cannot be changed by a caller
///     that casts it back to a mutable interface or concrete type.
/// </summary>
/// <remarks>
///     A read-only interface over a mutable backing store is only a convention. These assertions do what
///     a careless or hostile caller could do: cast to the common mutable shapes and try to change the
///     collection.
/// </remarks>
internal static class ReadOnlyAssertions
{
    /// <summary>
    ///     Asserts that a read-only list cannot be modified through a cast.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="list">The list under test; must contain at least one element.</param>
    public static void AssertCannotBeModified<T>(IReadOnlyList<T> list)
    {
        // The list must have an element so that replacing one can be tried
        Assert.NotEmpty(list);

        // The common mutable backing stores must not be what is exposed
        Assert.IsNotType<List<T>>(list);
        Assert.IsNotType<T[]>(list);

        // Whatever is exposed must refuse changes made through the mutable interfaces
        if (list is IList<T> mutable)
        {
            Assert.Throws<NotSupportedException>(() => mutable.Add(list[0]));
            Assert.Throws<NotSupportedException>(() => mutable[0] = list[^1]);
            Assert.Throws<NotSupportedException>(() => mutable.RemoveAt(0));
            Assert.Throws<NotSupportedException>(mutable.Clear);
        }
    }

    /// <summary>
    ///     Asserts that a read-only collection cannot be modified through a cast.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="collection">The collection under test; must contain at least one element.</param>
    public static void AssertCannotBeModified<T>(IReadOnlyCollection<T> collection)
    {
        Assert.NotEmpty(collection);

        // The common mutable backing stores must not be what is exposed
        Assert.IsNotType<List<T>>(collection);
        Assert.IsNotType<HashSet<T>>(collection);
        Assert.IsNotType<T[]>(collection);

        // Whatever is exposed must refuse changes made through the mutable interfaces
        if (collection is ICollection<T> mutable)
        {
            Assert.Throws<NotSupportedException>(() => mutable.Add(collection.First()));
            Assert.Throws<NotSupportedException>(mutable.Clear);
        }
    }

    /// <summary>
    ///     Asserts that a read-only dictionary cannot be modified through a cast.
    /// </summary>
    /// <typeparam name="TKey">The key type.</typeparam>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="dictionary">The dictionary under test; must contain at least one entry.</param>
    public static void AssertCannotBeModified<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> dictionary)
        where TKey : notnull
    {
        Assert.NotEmpty(dictionary);

        // The common mutable backing store must not be what is exposed
        Assert.IsNotType<Dictionary<TKey, TValue>>(dictionary);

        // Whatever is exposed must refuse changes made through the mutable interface
        if (dictionary is IDictionary<TKey, TValue> mutable)
        {
            var first = dictionary.First();
            Assert.Throws<NotSupportedException>(() => mutable[first.Key] = first.Value);
            Assert.Throws<NotSupportedException>(() => mutable.Add(first.Key, first.Value));
            Assert.Throws<NotSupportedException>(() => mutable.Remove(first.Key));
            Assert.Throws<NotSupportedException>(mutable.Clear);
        }
    }
}
