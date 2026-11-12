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
///     A uniquely named temporary directory that is deleted when disposed.
/// </summary>
/// <remarks>
///     Used by tests that need real files (discovery, engine, command line). Each instance owns
///     its own directory, so tests using it do not share state.
/// </remarks>
internal sealed class TemporaryDirectory : IDisposable
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TemporaryDirectory"/> class and creates the directory.
    /// </summary>
    public TemporaryDirectory()
    {
        FullPath = Path.Combine(Path.GetTempPath(), "lintmark-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(FullPath);
    }

    /// <summary>
    ///     Gets the full path of the directory.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    ///     Resolves a path relative to the directory.
    /// </summary>
    /// <param name="relativePath">A relative path using forward slashes.</param>
    /// <returns>The full path.</returns>
    public string PathOf(string relativePath) => Path.GetFullPath(Path.Combine(FullPath, relativePath));

    /// <summary>
    ///     Writes a text file, creating parent directories.
    /// </summary>
    /// <param name="relativePath">A relative path using forward slashes.</param>
    /// <param name="content">The file content.</param>
    /// <returns>The full path of the file.</returns>
    public string WriteFile(string relativePath, string content = "# Title\n\nText.\n")
    {
        var path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>
    ///     Writes a binary file, creating parent directories.
    /// </summary>
    /// <param name="relativePath">A relative path using forward slashes.</param>
    /// <param name="content">The file content.</param>
    /// <returns>The full path of the file.</returns>
    public string WriteBytes(string relativePath, byte[] content)
    {
        var path = PathOf(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return path;
    }

    /// <summary>
    ///     Deletes the directory and everything in it.
    /// </summary>
    public void Dispose()
    {
        try
        {
            Directory.Delete(FullPath, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a temporary directory that cannot be removed must not fail the test
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort, as above
        }
    }
}
