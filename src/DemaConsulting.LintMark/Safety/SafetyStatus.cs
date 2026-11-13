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

namespace DemaConsulting.LintMark.Safety;

/// <summary>
///     The outcome of the safety checks for one proposed rewrite of a file.
/// </summary>
/// <remarks>
///     Everything other than <see cref="Ok"/> means that the file must not be written.
/// </remarks>
internal enum SafetyStatus
{
    /// <summary>
    ///     Every check passed (or no check was needed because the text does not change).
    /// </summary>
    Ok,

    /// <summary>
    ///     The rewritten text would render differently.
    /// </summary>
    HtmlMismatch,

    /// <summary>
    ///     The rewritten text would change Pandoc structure that rendering does not show, or would be read
    ///     differently by the parser.
    /// </summary>
    StructureMismatch,

    /// <summary>
    ///     Formatting the rewritten text again would change it again.
    /// </summary>
    NotIdempotent
}
