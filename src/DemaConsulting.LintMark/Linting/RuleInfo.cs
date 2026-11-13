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

namespace DemaConsulting.LintMark.Linting;

/// <summary>
///     The registered facts about one rule identifier.
/// </summary>
/// <param name="Id">The canonical kebab-case rule identifier.</param>
/// <param name="Kind">Whether the rule is fixable by rewriting (<see cref="DiagnosticKind.Formatting"/>) or only reported.</param>
/// <param name="Description">A one-sentence description of what the rule reports or enforces.</param>
/// <param name="Aliases">The legacy rule-number aliases (such as <c>MD013</c>) that name the rule; empty when there are none.</param>
/// <param name="DefaultEnabled">Whether the rule is enabled when the configuration does not say.</param>
/// <param name="IsConfigurable">
///     Whether the <c>rules</c> setting of a configuration can switch the rule on or off. Rules that are always
///     applied, and the identifiers under which the tool reports its own failures, are not configurable.
/// </param>
/// <remarks>
///     Immutable; the alias array is an immutable array.
/// </remarks>
internal sealed record RuleInfo(
    string Id,
    DiagnosticKind Kind,
    string Description,
    ImmutableArray<string> Aliases,
    bool DefaultEnabled,
    bool IsConfigurable);
