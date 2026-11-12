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

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using DemaConsulting.LintMark.Configuration;

namespace DemaConsulting.LintMark.Tests;

/// <summary>
///     Unit tests for the ConfigLoader class: discovery, explicit files and caching.
/// </summary>
[Collection("Sequential")]
public class ConfigLoaderTests
{
    /// <summary>
    ///     Runs an action with the current directory set to a directory, restoring it afterwards.
    /// </summary>
    /// <param name="directory">The directory to make current.</param>
    /// <param name="action">The action to run.</param>
    private static void InDirectory(string directory, Action action)
    {
        var original = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(directory);
            action();
        }
        finally
        {
            Directory.SetCurrentDirectory(original);
        }
    }

    /// <summary>
    ///     Test that a defaults-only loader ignores configuration files.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDefaults_Load_IgnoresConfigFilesAndNeverProbes()
    {
        // Arrange: an invalid configuration file beside the Markdown file, and a path in a missing directory
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        var existing = directory.PathOf("a.md");
        var missing = directory.PathOf("missing/deeper/a.md");
        var loader = ConfigLoader.ForDefaults();

        // Act: load both
        var first = loader.Load(existing);
        var second = loader.Load(missing);

        // Assert: both are the defaults, attributed to no file, and nothing is reported
        Assert.Equal(LintMarkConfig.Default, first.Config);
        Assert.Null(first.SourcePath);
        Assert.Same(first, second);
        Assert.Empty(loader.InitialErrors);
    }

    /// <summary>
    ///     Test that discovery without any configuration file returns the defaults.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_NoConfigFile_ReturnsDefaults()
    {
        // Arrange: a repository root without a configuration file
        using var directory = new TemporaryDirectory();

        // Act: load a file in it
        var result = ConfigLoader.ForDiscovery().Load(directory.PathOf("docs/a.md"));

        // Assert: defaults and no source
        Assert.Equal(LintMarkConfig.Default, result.Config);
        Assert.Null(result.SourcePath);
    }

    /// <summary>
    ///     Test that a configuration file in the file's own directory is used.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_ConfigInOwnDirectory_UsesIt()
    {
        // Arrange: a configuration file beside the Markdown file
        using var directory = new TemporaryDirectory();
        var config = directory.WriteFile("docs/.lintmark.yaml", "line-length: 90\n");

        // Act: load the file
        var result = ConfigLoader.ForDiscovery().Load(directory.PathOf("docs/a.md"));

        // Assert: its settings are used
        Assert.Equal(90, result.Config!.LineLength);
        Assert.Equal(config, result.SourcePath);
    }

    /// <summary>
    ///     Test that a configuration file in an ancestor directory is used.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_ConfigInAncestor_UsesIt()
    {
        // Arrange: a configuration file two levels above
        using var directory = new TemporaryDirectory();
        var config = directory.WriteFile("docs/.lintmark.yaml", "line-length: 95\n");

        // Act: load a deeply nested file
        var result = ConfigLoader.ForDiscovery().Load(directory.PathOf("docs/a/b/c.md"));

        // Assert: the ancestor's settings are used
        Assert.Equal(95, result.Config!.LineLength);
        Assert.Equal(config, result.SourcePath);
    }

    /// <summary>
    ///     Test that the nearest configuration wins and a farther one is never read.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_NearestConfigWins_FartherNeverRead()
    {
        // Arrange: an invalid file in the root and a valid file nearer the Markdown file
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        directory.WriteFile("sub/.lintmark.yaml", "line-length: 85\n");

        // Act: load a file in the nearer directory
        var result = ConfigLoader.ForDiscovery().Load(directory.PathOf("sub/a.md"));

        // Assert: the nearer file is used and the farther file's problems are never reported
        Assert.Empty(result.Errors);
        Assert.Equal(85, result.Config!.LineLength);
    }

    /// <summary>
    ///     Test that a nearer partial configuration does not inherit from a farther one.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_NearerPartialConfig_DoesNotInheritFarther()
    {
        // Arrange: a root file setting the line length and a nearer file setting something else
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 70\n");
        directory.WriteFile("sub/.lintmark.yaml", "wrap: sentence\n");

        // Act: load a file in the nearer directory
        var result = ConfigLoader.ForDiscovery().Load(directory.PathOf("sub/a.md"));

        // Assert: the line length is the default, not the farther file's value
        Assert.Equal(120, result.Config!.LineLength);
        Assert.Equal(WrapMode.Sentence, result.Config.Wrap);
    }

    /// <summary>
    ///     Test that a configuration file in the repository root itself is found.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_ConfigInRepositoryRoot_IsFound()
    {
        // Arrange: a repository root (it holds a .git directory) with a configuration file
        using var directory = new TemporaryDirectory();
        var config = directory.WriteFile(".lintmark.yaml", "line-length: 77\n");

        // Act: load a nested file
        var result = ConfigLoader.ForDiscovery().Load(directory.PathOf("a/b/c.md"));

        // Assert: the root's file is used
        Assert.Equal(77, result.Config!.LineLength);
        Assert.Equal(config, result.SourcePath);
    }

    /// <summary>
    ///     Test that a configuration file above a repository root is not used.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_GitDirectoryStopsSearch_ConfigAboveIgnored()
    {
        // Arrange: a configuration file above a directory that holds a .git directory
        using var directory = new TemporaryDirectory(repositoryRoot: false);
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        Directory.CreateDirectory(directory.PathOf("repo/.git"));

        // Act: load a file inside the repository
        var result = ConfigLoader.ForDiscovery().Load(directory.PathOf("repo/sub/a.md"));

        // Assert: the file above the repository is not used or read
        Assert.Equal(LintMarkConfig.Default, result.Config);
        Assert.Null(result.SourcePath);
    }

    /// <summary>
    ///     Test that a .git file, as used by linked working trees and submodules, stops the search like a .git directory.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_GitFileStopsSearch_ConfigAboveIgnored()
    {
        // Arrange: a configuration file above a directory that holds a .git file
        using var directory = new TemporaryDirectory(repositoryRoot: false);
        directory.WriteFile(".lintmark.yaml", "bogus: 1\n");
        directory.WriteFile("repo/.git", "refers to a repository stored elsewhere\n");

        // Act: load a file inside the repository
        var result = ConfigLoader.ForDiscovery().Load(directory.PathOf("repo/sub/a.md"));

        // Assert: the file above is not used
        Assert.Equal(LintMarkConfig.Default, result.Config);
    }

    /// <summary>
    ///     Test that a .git entry in a nearer folder stops the search there.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_GitInNearerFolder_StopsThere()
    {
        // Arrange: a root file, and an inner folder with a .git directory between it and the Markdown file
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 60\n");
        Directory.CreateDirectory(directory.PathOf("inner/.git"));

        // Act: load a file inside the inner folder
        var result = ConfigLoader.ForDiscovery().Load(directory.PathOf("inner/deep/a.md"));

        // Assert: the root's file is beyond the boundary
        Assert.Equal(LintMarkConfig.Default, result.Config);
    }

    /// <summary>
    ///     Test that a configuration file in the same directory as a .git entry is found before the boundary applies.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_ConfigBesideGitEntry_IsFound()
    {
        // Arrange: a nested repository with its own configuration, under a repository with another
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 60\n");
        Directory.CreateDirectory(directory.PathOf("inner/.git"));
        directory.WriteFile("inner/.lintmark.yaml", "line-length: 65\n");

        // Act: load a file in the nested repository
        var result = ConfigLoader.ForDiscovery().Load(directory.PathOf("inner/a.md"));

        // Assert: the nested repository's own file is used
        Assert.Equal(65, result.Config!.LineLength);
    }

    /// <summary>
    ///     Test that without any repository marker the search continues to the parent folders.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_NoGitAnywhere_ConfigInParentFound()
    {
        // Arrange: a folder tree without any .git entry and a configuration file at its top
        using var directory = new TemporaryDirectory(repositoryRoot: false);
        directory.WriteFile(".lintmark.yaml", "line-length: 66\n");

        // Act: load a nested file
        var result = ConfigLoader.ForDiscovery().Load(directory.PathOf("a/b/c.md"));

        // Assert: the parent's file is found
        Assert.Equal(66, result.Config!.LineLength);
    }

    /// <summary>
    ///     Test that a directory with the name of the configuration file is not a configuration.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_DirectoryNamedLikeConfig_IsSkipped()
    {
        // Arrange: a directory named .lintmark.yaml nearer the file, and a real file in the root
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 88\n");
        Directory.CreateDirectory(directory.PathOf("sub/.lintmark.yaml"));

        // Act: load a file in the folder holding the directory
        var result = ConfigLoader.ForDiscovery().Load(directory.PathOf("sub/a.md"));

        // Assert: the directory is skipped and the root's file is used
        Assert.Equal(88, result.Config!.LineLength);
    }

    /// <summary>
    ///     Test that a file in a directory that does not exist still finds the configuration above it.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_FileInMissingDirectory_StillSearchesUpward()
    {
        // Arrange: a root configuration file and a path whose folders do not exist
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 99\n");

        // Act: load the path
        var result = ConfigLoader.ForDiscovery().Load(directory.PathOf("missing/deeper/a.md"));

        // Assert: the root's file is used
        Assert.Equal(99, result.Config!.LineLength);
    }

    /// <summary>
    ///     Test that a relative Markdown path is resolved against the current directory.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_RelativeMarkdownPath_ResolvedAgainstCurrentDirectory()
    {
        // Arrange: a configuration file in a sub folder of the temporary directory
        using var directory = new TemporaryDirectory();
        directory.WriteFile("sub/.lintmark.yaml", "line-length: 91\n");
        var loader = ConfigLoader.ForDiscovery();

        // Act: load a relative path while that directory is current
        ConfigLoadResult? result = null;
        InDirectory(directory.FullPath, () => result = loader.Load("sub/a.md"));

        // Assert: the relative path found the file
        Assert.Equal(91, result!.Config!.LineLength);
    }

    /// <summary>
    ///     Test that backslash separators are accepted on every platform.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_BackslashSeparators_AreNormalized()
    {
        // Arrange: a configuration file and a path written with backslashes
        using var directory = new TemporaryDirectory();
        directory.WriteFile("sub/.lintmark.yaml", "line-length: 92\n");
        var path = directory.FullPath + "\\sub\\a.md";

        // Act: load the path
        var result = ConfigLoader.ForDiscovery().Load(path);

        // Assert: the separators were normalized
        Assert.Equal(92, result.Config!.LineLength);
    }

    /// <summary>
    ///     Test that two files in the same directory share one result instance.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_SameDirectoryTwice_ReturnsSameResultInstance()
    {
        // Arrange: a configuration file and a loader
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 93\n");
        var loader = ConfigLoader.ForDiscovery();

        // Act: load two files of one directory
        var first = loader.Load(directory.PathOf("docs/a.md"));
        var second = loader.Load(directory.PathOf("docs/b.md"));

        // Assert: one result is shared
        Assert.Same(first, second);
    }

    /// <summary>
    ///     Test that a configuration changed after the first load is not read again.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_ConfigChangedAfterFirstLoad_CachedResultKept()
    {
        // Arrange: a configuration file that is loaded once
        using var directory = new TemporaryDirectory();
        var config = directory.WriteFile(".lintmark.yaml", "line-length: 94\n");
        var loader = ConfigLoader.ForDiscovery();
        var first = loader.Load(directory.PathOf("a.md"));

        // Act: change the file and load again
        File.WriteAllText(config, "line-length: 50\n");
        var second = loader.Load(directory.PathOf("b.md"));

        // Assert: the cached result is used
        Assert.Equal(94, second.Config!.LineLength);
        Assert.Same(first, second);
    }

    /// <summary>
    ///     Test that many threads loading from many directories get the sequential results and share one result per file.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_ManyThreads_ResultsEqualSequential()
    {
        // Arrange: a root file, two nested files, and many directories below them (some with an invalid file)
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 71\n");
        directory.WriteFile("a/.lintmark.yaml", "line-length: 72\n");
        directory.WriteFile("b/.lintmark.yaml", "bogus: 1\n");
        var files = Enumerable.Range(0, 60)
            .Select(i => directory.PathOf($"{(char)('a' + (i % 3))}/d{i % 10}/f{i}.md"))
            .Concat(Enumerable.Range(0, 20).Select(i => directory.PathOf($"top{i % 4}/f{i}.md")))
            .ToArray();
        var expected = files.Select(f => ConfigLoader.ForDiscovery().Load(f)).Select(Describe).ToArray();
        var loader = ConfigLoader.ForDiscovery();

        // Act: load every file many times from many threads
        var results = new ConfigLoadResult[files.Length * 10];
        Parallel.For(0, results.Length, new ParallelOptions { MaxDegreeOfParallelism = 16 }, index => results[index] = loader.Load(files[index % files.Length]));

        // Assert: every result equals the sequential one, and there is one instance per configuration file
        for (var i = 0; i < results.Length; i++)
        {
            Assert.Equal(expected[i % files.Length], Describe(results[i]));
        }

        Assert.Equal(3, results.Cast<object>().Distinct(ReferenceEqualityComparer.Instance).Count());
        return;

        static string Describe(ConfigLoadResult r) =>
            $"{r.SourcePath}|{r.Config?.LineLength}|{string.Join(";", r.Errors.Select(e => $"{e.Line}:{e.Column}:{e.Message}"))}";
    }

    /// <summary>
    ///     Test the discovery walk for several folder layouts.
    /// </summary>
    [Fact]
    public void ConfigLoader_FindConfigFile_Variants_ReturnsExpectedPath()
    {
        // Arrange: a repository with nested configuration files and a nested repository without one
        using var directory = new TemporaryDirectory();
        var root = directory.WriteFile(".lintmark.yaml", "line-length: 1\n");
        var docs = directory.WriteFile("docs/.lintmark.yaml", "line-length: 2\n");
        Directory.CreateDirectory(directory.PathOf("vendor/.git"));
        Directory.CreateDirectory(directory.PathOf("plain/deep"));

        // Act & Assert: nearest wins, the walk passes through folders without a file, and a .git entry ends it
        Assert.Equal(docs, ConfigLoader.FindConfigFile(directory.PathOf("docs")));
        Assert.Equal(docs, ConfigLoader.FindConfigFile(directory.PathOf("docs/a/b")));
        Assert.Equal(docs, ConfigLoader.FindConfigFile(directory.PathOf("docs") + Path.DirectorySeparatorChar));
        Assert.Equal(root, ConfigLoader.FindConfigFile(directory.PathOf("plain/deep")));
        Assert.Equal(root, ConfigLoader.FindConfigFile(directory.FullPath));
        Assert.Null(ConfigLoader.FindConfigFile(directory.PathOf("vendor/x")));
    }

    /// <summary>
    ///     Test that the discovery walk rejects a null or empty directory.
    /// </summary>
    [Fact]
    public void ConfigLoader_FindConfigFile_NullOrEmpty_Throws()
    {
        // Act & Assert: the argument is validated
        Assert.Throws<ArgumentNullException>(() => ConfigLoader.FindConfigFile(null!));
        Assert.Throws<ArgumentException>(() => ConfigLoader.FindConfigFile(string.Empty));
    }

    /// <summary>
    ///     Test that an explicit file loader returns the file's configuration for any Markdown file.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForFile_Load_AnyMarkdownFile_ReturnsFileConfig()
    {
        // Arrange: an explicit configuration file outside the Markdown file's folder
        using var directory = new TemporaryDirectory();
        var config = directory.WriteFile("conf/custom.yaml", "line-length: 83\n");
        var loader = ConfigLoader.ForFile(config);

        // Act: load files from unrelated places
        var first = loader.Load(directory.PathOf("a.md"));
        var second = loader.Load(directory.PathOf("x/y/z.md"));

        // Assert: both get the file's settings from one shared result
        Assert.Equal(83, first.Config!.LineLength);
        Assert.Equal(config, first.SourcePath);
        Assert.Same(first, second);
        Assert.Empty(loader.InitialErrors);
    }

    /// <summary>
    ///     Test that a relative explicit file is resolved against the current directory.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForFile_RelativeFile_ResolvedAgainstCurrentDirectory()
    {
        // Arrange: a configuration file in a sub folder of the temporary directory
        using var directory = new TemporaryDirectory();
        directory.WriteFile("conf/custom.yaml", "line-length: 84\n");

        // Act: create the loader with a relative path (using backslashes) while that directory is current
        ConfigLoader? loader = null;
        InDirectory(directory.FullPath, () => loader = ConfigLoader.ForFile("conf\\custom.yaml"));

        // Assert: the file was found and is reported by its full path
        var result = loader!.Load("anything.md");
        Assert.Equal(84, result.Config!.LineLength);
        Assert.Equal(directory.PathOf("conf/custom.yaml"), result.SourcePath);
    }

    /// <summary>
    ///     Test that a missing explicit file is reported at line one, column one.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForFile_MissingFile_ReportsNotFoundAt1x1()
    {
        // Arrange: a path that does not exist
        using var directory = new TemporaryDirectory();
        var missing = directory.PathOf("missing.yaml");

        // Act: create the loader
        var loader = ConfigLoader.ForFile(missing);

        // Assert: one problem at the start of the file, available even before any file is processed
        var error = Assert.Single(loader.InitialErrors);
        Assert.Equal((missing, 1, 1, "Configuration file not found"), (error.Path, error.Line, error.Column, error.Message));
        Assert.Null(loader.Load("a.md").Config);
    }

    /// <summary>
    ///     Test that a directory given as the explicit file is reported.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForFile_DirectoryAsFile_ReportsCannotRead()
    {
        // Arrange: a directory where a file is expected
        using var directory = new TemporaryDirectory();

        // Act: create the loader
        var loader = ConfigLoader.ForFile(directory.FullPath);

        // Assert: reported as unreadable
        var error = Assert.Single(loader.InitialErrors);
        Assert.Contains("Cannot read configuration file", error.Message);
        Assert.Equal((1, 1), (error.Line, error.Column));
    }

    /// <summary>
    ///     Test that a file locked against reading is reported (where the platform enforces locks).
    /// </summary>
    [Fact]
    public void ConfigLoader_ForFile_LockedFile_ReportsCannotRead()
    {
        // Arrange: a file held open exclusively; only Windows enforces this for every reader
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Exclusive file locks are enforced on Windows only");
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile("locked.yaml", "line-length: 80\n");
        using var held = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        // Act: create the loader
        var loader = ConfigLoader.ForFile(file);

        // Assert: reported as unreadable rather than thrown
        var error = Assert.Single(loader.InitialErrors);
        Assert.Contains("Cannot read configuration file", error.Message);
    }

    /// <summary>
    ///     Test that an explicit file is used even when a discoverable file exists.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForFile_DiscoverableFilePresent_IgnoresDiscoverableFile()
    {
        // Arrange: a discoverable file beside the Markdown file and a different explicit file
        using var directory = new TemporaryDirectory();
        directory.WriteFile(".lintmark.yaml", "line-length: 50\n");
        var explicitFile = directory.WriteFile("other.yaml", "line-length: 150\n");

        // Act: load a file next to the discoverable one
        var result = ConfigLoader.ForFile(explicitFile).Load(directory.PathOf("a.md"));

        // Assert: only the explicit file counts
        Assert.Equal(150, result.Config!.LineLength);
    }

    /// <summary>
    ///     Test that the problems of an invalid explicit file are the initial errors and the result for every file.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForFile_InitialErrors_ContainFileErrors()
    {
        // Arrange: an invalid explicit file
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile("bad.yaml", "bogus: 1\nwrap: nope\n");

        // Act: create the loader
        var loader = ConfigLoader.ForFile(file);

        // Assert: the problems are known up front and returned for every file
        Assert.Equal(2, loader.InitialErrors.Length);
        Assert.Equal(loader.InitialErrors, loader.Load("a.md").Errors);
        Assert.Empty(ConfigLoader.ForDiscovery().InitialErrors);
        Assert.Empty(ConfigLoader.ForDefaults().InitialErrors);
    }

    /// <summary>
    ///     Test that a file larger than the limit is reported without being parsed.
    /// </summary>
    [Fact]
    public void ConfigLoader_LoadFile_TooLarge_ReportsSizeError()
    {
        // Arrange: a file one byte over the limit
        using var directory = new TemporaryDirectory();
        var file = directory.WriteBytes("big.yaml", Enumerable.Repeat((byte)'#', ConfigLoader.MaxFileBytes + 1).ToArray());

        // Act: load it
        var result = ConfigLoader.LoadFile(file);

        // Assert: reported at the start of the file
        var error = Assert.Single(result.Errors);
        Assert.Equal((1, 1), (error.Line, error.Column));
        Assert.Contains("larger than 1 MiB", error.Message);
    }

    /// <summary>
    ///     Test that a file of exactly the size limit is read and accepted through the file reader.
    /// </summary>
    [Fact]
    public void ConfigLoader_LoadFile_AtSizeLimit_Accepted()
    {
        // Arrange: a comment-only file of exactly the limit
        using var directory = new TemporaryDirectory();
        var file = directory.WriteBytes("limit.yaml", Enumerable.Repeat((byte)'#', ConfigLoader.MaxFileBytes).ToArray());

        // Act: load it
        var result = ConfigLoader.LoadFile(file);

        // Assert: it is a valid, empty configuration
        Assert.Empty(result.Errors);
        Assert.Equal(LintMarkConfig.Default, result.Config);
    }

    /// <summary>
    ///     Test that an explicit file of exactly the size limit gives no initial errors.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForFile_AtSizeLimit_HasNoInitialErrors()
    {
        // Arrange: a comment-only file of exactly the limit
        using var directory = new TemporaryDirectory();
        var file = directory.WriteBytes("limit.yaml", Enumerable.Repeat((byte)'#', ConfigLoader.MaxFileBytes).ToArray());

        // Act: create the loader
        var loader = ConfigLoader.ForFile(file);

        // Assert: nothing is reported and the defaults apply to every file
        Assert.Empty(loader.InitialErrors);
        Assert.Equal(LintMarkConfig.Default, loader.Load("a.md").Config);
    }

    /// <summary>
    ///     Test that a Markdown path that cannot be resolved has no configuration.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_PathWithNullCharacter_ReturnsDefaults()
    {
        // Arrange: a path no file system accepts
        var loader = ConfigLoader.ForDiscovery();

        // Act: load it
        var result = loader.Load("docs/a\0b.md");

        // Assert: the defaults apply and nothing is thrown or reported
        Assert.Equal(LintMarkConfig.Default, result.Config);
        Assert.Null(result.SourcePath);
        Assert.Empty(result.Errors);
    }

    /// <summary>
    ///     Test that a Markdown path without a directory, the root of the file system, has no configuration.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForDiscovery_Load_FileSystemRootPath_ReturnsDefaults()
    {
        // Arrange: the root of the file system, which has no containing directory
        var root = Path.GetPathRoot(Directory.GetCurrentDirectory())!;

        // Act: load it
        var result = ConfigLoader.ForDiscovery().Load(root);

        // Assert: the defaults apply
        Assert.Equal(LintMarkConfig.Default, result.Config);
        Assert.Null(result.SourcePath);
    }

    /// <summary>
    ///     Test that a directory that cannot be resolved has no configuration file.
    /// </summary>
    [Fact]
    public void ConfigLoader_FindConfigFile_PathWithNullCharacter_ReturnsNull()
    {
        // Act & Assert: a path no file system accepts finds nothing and does not throw
        Assert.Null(ConfigLoader.FindConfigFile("docs/a\0b"));
    }

    /// <summary>
    ///     Test that an explicit file whose path cannot be resolved is reported against the text as given.
    /// </summary>
    [Fact]
    public void ConfigLoader_LoadFile_PathWithNullCharacter_ReportsPathNotValid()
    {
        // Act: load a path no file system accepts
        var result = ConfigLoader.LoadFile("conf/a\0b.yaml");

        // Assert: one problem at the start of the file, named by the given path
        var error = Assert.Single(result.Errors);
        Assert.Equal(("conf/a\0b.yaml", 1, 1), (error.Path, error.Line, error.Column));
        Assert.Contains("the path is not valid", error.Message);
        Assert.Null(result.Config);
    }

    /// <summary>
    ///     Test that an explicit file that the current user may not read is reported as denied.
    /// </summary>
    [Fact]
    public void ConfigLoader_LoadFile_AccessDenied_ReportsAccessDenied()
    {
        // Arrange: a file whose read permission is removed for the current user
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile("denied.yaml", "line-length: 80\n");
        var restore = DenyRead(file);
        try
        {
            // A process with full rights (for example a superuser) can still read the file; nothing is testable then
            Assert.SkipWhen(CanRead(file), "The current user can read the file despite the removed permission");

            // Act: load it
            var result = ConfigLoader.LoadFile(file);

            // Assert: reported as denied at the start of the file
            var error = Assert.Single(result.Errors);
            Assert.Equal((1, 1, "Access to the configuration file is denied"), (error.Line, error.Column, error.Message));
        }
        finally
        {
            restore();
        }
    }

    /// <summary>
    ///     Removes the read permission of a file for the current user.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>An action that restores the permission so the file can be deleted.</returns>
    private static Action DenyRead(string file)
    {
        if (OperatingSystem.IsWindows())
        {
            return DenyReadWindows(file);
        }
        else
        {
            return DenyReadUnix(file);
        }
    }

    /// <summary>
    ///     Adds a deny-read entry to the access list of a file for the current user.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>An action that removes the entry again.</returns>
    [SupportedOSPlatform("windows")]
    private static Action DenyReadWindows(string file)
    {
        var rule = new FileSystemAccessRule(WindowsIdentity.GetCurrent().User!, FileSystemRights.ReadData, AccessControlType.Deny);
        var info = new FileInfo(file);
        var security = info.GetAccessControl();
        security.AddAccessRule(rule);
        info.SetAccessControl(security);
        return () =>
        {
            var current = info.GetAccessControl();
            current.RemoveAccessRule(rule);
            info.SetAccessControl(current);
        };
    }

    /// <summary>
    ///     Removes all permissions of a file.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns>An action that restores read and write permission for the owner.</returns>
    [UnsupportedOSPlatform("windows")]
    private static Action DenyReadUnix(string file)
    {
        File.SetUnixFileMode(file, UnixFileMode.None);
        return () => File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    /// <summary>
    ///     Determines whether a file can be opened for reading.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <returns><see langword="true"/> when it can be read.</returns>
    private static bool CanRead(string file)
    {
        try
        {
            using var stream = File.OpenRead(file);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Test that loading a valid file reports its full path as the source.
    /// </summary>
    [Fact]
    public void ConfigLoader_LoadFile_ValidFile_ReturnsConfigWithFullPath()
    {
        // Arrange: a valid file
        using var directory = new TemporaryDirectory();
        var file = directory.WriteFile("ok.yaml", "bullet: plus\n");

        // Act: load it
        var result = ConfigLoader.LoadFile(file);

        // Assert: the settings and the source path
        Assert.Equal(BulletMarker.Plus, result.Config!.Bullet);
        Assert.Equal(file, result.SourcePath);
    }

    /// <summary>
    ///     Test that Load rejects a null or empty Markdown path.
    /// </summary>
    [Fact]
    public void ConfigLoader_Load_NullOrEmptyPath_Throws()
    {
        // Arrange: one loader per mode
        var loaders = new[] { ConfigLoader.ForDefaults(), ConfigLoader.ForDiscovery() };

        // Act & Assert: the path is validated in every mode
        foreach (var loader in loaders)
        {
            Assert.Throws<ArgumentNullException>(() => loader.Load(null!));
            Assert.Throws<ArgumentException>(() => loader.Load(string.Empty));
        }
    }

    /// <summary>
    ///     Test that the explicit file factory rejects a null or blank path.
    /// </summary>
    [Fact]
    public void ConfigLoader_ForFile_NullOrBlank_Throws()
    {
        // Act & Assert: the path is validated
        Assert.Throws<ArgumentNullException>(() => ConfigLoader.ForFile(null!));
        Assert.Throws<ArgumentException>(() => ConfigLoader.ForFile("  "));
        Assert.Throws<ArgumentNullException>(() => ConfigLoader.LoadFile(null!));
        Assert.Throws<ArgumentException>(() => ConfigLoader.LoadFile(string.Empty));
    }
}
