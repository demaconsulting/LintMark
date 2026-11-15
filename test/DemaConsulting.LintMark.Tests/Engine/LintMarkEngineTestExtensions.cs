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

using DemaConsulting.LintMark.Configuration;
using DemaConsulting.LintMark.Engine;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Lets tests process files that were all named explicitly by path, which the product does not need.
/// </summary>
internal static class LintMarkEngineTestExtensions
{
    /// <summary>
    ///     Processes files with the built-in default configuration.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="files">Paths of the files; every file is treated as named on the command line.</param>
    /// <returns>One result per file, in path order.</returns>
    public static IReadOnlyList<FileResult> Process(this LintMarkEngine engine, IReadOnlyList<string> files) =>
        engine.Process(files, ConfigLoader.ForDefaults(), null);

    /// <summary>
    ///     Processes files that were all named explicitly.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <param name="files">Paths of the files; every file is treated as named on the command line.</param>
    /// <param name="loader">Supplies the configuration that applies to each file.</param>
    /// <param name="editorConfig">Supplies the line ending from the editor settings, or <see langword="null"/> to ignore them.</param>
    /// <returns>One result per file, in path order.</returns>
    public static IReadOnlyList<FileResult> Process(
        this LintMarkEngine engine,
        IReadOnlyList<string> files,
        ConfigLoader loader,
        EditorConfigResolver? editorConfig) =>
        engine.Process([.. files.Select(static file => new DiscoveredFile(file, IsExplicit: true))], loader, editorConfig);
}
