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
using DemaConsulting.LintMark.Document;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Subsystem tests of the Configuration subsystem: its configuration loader and editor settings resolver
///     are driven together from outside, with real files and no substitutes.
/// </summary>
public class ConfigurationSubsystemTests
{
    /// <summary>
    ///     Test that the nearest configuration file applies per file, that an explicit file replaces discovery, and that
    ///     the defaults-only loader reads no file.
    /// </summary>
    [Fact]
    public void ConfigurationSubsystem_Discovery_NearestExplicitAndNone_SelectTheConfiguration()
    {
        // Arrange: a configuration in the root, a nearer one in a folder, and another file to name explicitly
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 70\n");
        directory.WriteFile("sub/.lintmark.yaml", "line-length: 85\n");
        var explicitFile = directory.WriteFile("other.yaml", "line-length: 95\n");
        var rootMarkdown = directory.PathOf("a.md");
        var nestedMarkdown = directory.PathOf("sub/a.md");

        // Act: load each Markdown file with discovery, with the explicit file and with the defaults only
        var discovery = ConfigLoader.ForDiscovery();
        var explicitLoader = ConfigLoader.ForFile(explicitFile);
        var defaults = ConfigLoader.ForDefaults();

        // Assert: discovery picks the nearest file per Markdown file, the explicit file applies to both, the defaults to none
        Assert.Equal(70, discovery.Load(rootMarkdown).Config!.LineLength);
        Assert.Equal(85, discovery.Load(nestedMarkdown).Config!.LineLength);
        Assert.Equal(95, explicitLoader.Load(rootMarkdown).Config!.LineLength);
        Assert.Equal(95, explicitLoader.Load(nestedMarkdown).Config!.LineLength);
        Assert.Equal(LintMarkConfig.Default, defaults.Load(nestedMarkdown).Config);
        Assert.Null(defaults.Load(nestedMarkdown).SourcePath);
    }

    /// <summary>
    ///     Test that a configuration that gives every setting is read into typed values, that rule switches work by id and
    ///     by number, and that a dropped rule number is accepted and ignored.
    /// </summary>
    [Fact]
    public void ConfigurationSubsystem_Settings_FullSample_ParsesTypedValuesAndRuleSwitches()
    {
        // Arrange: a configuration file that gives every setting
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile(".lintmark.yaml", ConfigurationSamples.FullSample);

        // Act: load it for a Markdown file beside it
        var result = ConfigLoader.ForDiscovery().Load(directory.PathOf("a.md"));

        // Assert: no problem, typed values, and rule switches that are keyed by the canonical id
        Assert.Empty(result.Errors);
        Assert.Equal(file, result.SourcePath);
        var config = result.Config!;
        Assert.Equal(100, config.LineLength);
        Assert.Equal(WrapMode.Sentence, config.Wrap);
        Assert.Equal(LineEnding.CrLf, config.EndOfLine);
        Assert.False(config.FinalNewline);
        Assert.Equal(BulletMarker.Asterisk, config.Bullet);
        Assert.Equal(4, config.ListIndent);
        Assert.Equal(TableStyle.Aligned, config.Tables);
        Assert.Equal(new LinkRefactorOptions(true, 80, false, true, 25, "<!-- Links -->"), config.LinkRefactor);
        Assert.False(config.Rules["line-length"]);
        Assert.False(config.Rules["heading-increment"]);
        Assert.True(config.Rules["descriptive-link-text"]);
        Assert.True(config.Rules["emphasis-space"]);
    }

    /// <summary>
    ///     Test that every invalid setting is reported with its position, once, however many files use the configuration.
    /// </summary>
    [Fact]
    public void ConfigurationSubsystem_Validation_InvalidSettings_ReportEveryProblemOnceWithPosition()
    {
        // Arrange: a configuration with an unknown setting, an invalid value and an unknown rule
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "color: red\nwrap: nope\nrules:\n  no-such-rule: false\n");
        var loader = ConfigLoader.ForDiscovery();

        // Act: load two Markdown files that use the same configuration
        var first = loader.Load(directory.PathOf("a.md"));
        var second = loader.Load(directory.PathOf("docs/b.md"));

        // Assert: three problems at their positions, shared by both files so that a reporter can show each one once
        Assert.Null(first.Config);
        Assert.Same(first, second);
        Assert.Equal(
            [(1, 1, "Unknown setting 'color'"), (2, 7, "Invalid value 'nope' for setting 'wrap'; expected one of: fill, sentence, semantic"), (4, 3, "Unknown rule 'no-such-rule'")],
            first.Errors.Select(e => (e.Line, e.Column, e.Message)));
        Assert.All(first.Errors, e => Assert.EndsWith(".lintmark.yaml", e.Path, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that the line ending comes from the configuration when it names one, and otherwise from the editor settings
    ///     that apply to the Markdown file.
    /// </summary>
    [Fact]
    public void ConfigurationSubsystem_LineEnding_ConfigAndEditorConfig_ResolveSeparatelyPerFile()
    {
        // Arrange: editor settings that ask for CRLF in Markdown files, and a configuration that asks for LF in a folder only
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".editorconfig", "root = true\n\n[*.md]\nend_of_line = crlf\n");
        directory.WriteFile("sub/.lintmark.yaml", "end-of-line: lf\n");
        var loader = ConfigLoader.ForDiscovery();
        var resolver = new EditorConfigResolver();

        // Act: load and resolve for a file of each folder
        var rootConfig = loader.Load(directory.PathOf("a.md")).Config!;
        var subConfig = loader.Load(directory.PathOf("sub/a.md")).Config!;
        var rootEditor = resolver.Resolve(directory.PathOf("a.md"));
        var subEditor = resolver.Resolve(directory.PathOf("sub/a.md"));

        // Assert: the root names no ending, so the editor settings decide; the folder's configuration names LF
        Assert.Null(rootConfig.EndOfLine);
        Assert.Equal(LineEnding.CrLf, rootEditor.EndOfLine);
        Assert.Equal(LineEnding.Lf, subConfig.EndOfLine);
        Assert.Equal(LineEnding.CrLf, subEditor.EndOfLine);
        Assert.Null(rootEditor.ErrorMessage);
    }

    /// <summary>
    ///     Test that the ignore patterns of a configuration are read, and that without any the default patterns apply.
    /// </summary>
    [Fact]
    public void ConfigurationSubsystem_Ignore_Patterns_AreReadAndDefaultsApplyOtherwise()
    {
        // Arrange: a configuration with two ignore patterns in a folder and none in the root
        using var directory = new TemporaryDirectory();
        directory.WriteFile("docs/.lintmark.yaml", "ignore:\n  - \"drafts/**\"\n  - \"**/vendor/**\"\n");
        var loader = ConfigLoader.ForDiscovery();

        // Act: load a file of each folder
        var withPatterns = loader.Load(directory.PathOf("docs/a.md")).Config!;
        var withoutPatterns = loader.Load(directory.PathOf("a.md")).Config!;

        // Assert: the patterns are exactly those of the file, and the defaults skip dependency and generated folders
        Assert.Equal(["drafts/**", "**/vendor/**"], withPatterns.Ignore);
        Assert.Equal(LintMarkConfig.DefaultIgnore, withoutPatterns.Ignore);
        Assert.Contains("node_modules/**", withoutPatterns.Ignore);
        Assert.Contains("**/generated/**", withoutPatterns.Ignore);
    }
}
