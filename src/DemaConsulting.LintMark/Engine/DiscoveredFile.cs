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

namespace DemaConsulting.LintMark.Engine;

/// <summary>
///     A file selected for processing, and whether the command line named it.
/// </summary>
/// <remarks>
///     The distinction exists because a file the user names is a deliberate request: the built-in exclusion of
///     <c>node_modules</c> and <c>.git</c> and the <c>ignore</c> patterns of the configuration do not apply to
///     it (only <c>!</c> excludes on the command line do). Immutable value type; safe for concurrent use. A
///     default instance has no path and bypasses the validation of the constructor.
/// </remarks>
internal readonly record struct DiscoveredFile
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DiscoveredFile"/> struct.
    /// </summary>
    /// <param name="path">The full path of the file. Must not be null or empty.</param>
    /// <param name="isExplicit">
    ///     <see langword="true"/> when the file was named on the command line (as an existing file, or as a path
    ///     without wildcards); <see langword="false"/> when it was found through a directory, a glob or the default pattern.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is empty.</exception>
    public DiscoveredFile(string path, bool isExplicit)
    {
        // Validate the path so that a file without a path can never be created
        ArgumentException.ThrowIfNullOrEmpty(path);

        Path = path;
        IsExplicit = isExplicit;
    }

    /// <summary>
    ///     Gets the full path of the file.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///     Gets a value indicating whether the file was named on the command line.
    /// </summary>
    public bool IsExplicit { get; }
}
