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

namespace DemaConsulting.LintMark.Verify.Reporting;

/// <summary>
///     Shows paths the way the reports do: relative to a directory when below it, with forward slashes.
/// </summary>
internal static class DisplayPath
{
    /// <summary>
    ///     Creates the display form of a path.
    /// </summary>
    /// <param name="path">The full path.</param>
    /// <param name="baseDirectory">The directory to make the path relative to.</param>
    /// <returns>The relative path with forward slashes when the file is below the directory; otherwise the full path with forward slashes.</returns>
    public static string Of(string path, string baseDirectory)
    {
        var relative = Path.GetRelativePath(baseDirectory, path);

        // A path below the directory stays relative, even when its first segment merely starts with two dots (..data)
        var outside = relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            || relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal)
            || Path.IsPathRooted(relative);
        var shown = outside ? path : relative;
        return shown.Replace('\\', '/');
    }
}
