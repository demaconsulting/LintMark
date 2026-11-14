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

using System.Globalization;
using DemaConsulting.LintMark.Configuration;
using YamlDotNet.Serialization;

namespace DemaConsulting.LintMark.Verify.Options;

/// <summary>
///     One combination of LintMark settings to verify under, with a label for the reports.
/// </summary>
/// <remarks>
///     <para>
///         The settings are merged over an optional base configuration file and written to a temporary
///         <c>.lintmark.yaml</c> in the temporary directory of the verifier (never next to an input), which
///         the product's own configuration loader then reads. Using the real loader means an invalid setting
///         shows up as the same configuration error the tool itself would give.
///     </para>
///     <para>
///         The special set <see cref="RepoConfiguration"/> discovers the configuration and the editor settings
///         for each file instead. Immutable; the loader it creates is safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class OptionSet
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="OptionSet"/> class.
    /// </summary>
    /// <param name="label">The short description of the set shown in the reports. Must not be blank.</param>
    /// <param name="values">The settings to apply, with dotted keys for nested settings. Must not be null.</param>
    /// <param name="baseConfigFile">The base configuration file, or <see langword="null"/> for none.</param>
    /// <param name="discoverPerFile">Whether the configuration is discovered per file (the settings are then ignored).</param>
    public OptionSet(string label, IReadOnlyList<KeyValuePair<string, string>> values, string? baseConfigFile = null, bool discoverPerFile = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(values);
        Label = label;
        Values = values;
        BaseConfigFile = baseConfigFile;
        DiscoverPerFile = discoverPerFile;
    }

    /// <summary>
    ///     Gets the short description of the set.
    /// </summary>
    public string Label { get; }

    /// <summary>
    ///     Gets the settings, in the order they are applied (a later entry replaces an earlier one with the same key).
    /// </summary>
    public IReadOnlyList<KeyValuePair<string, string>> Values { get; }

    /// <summary>
    ///     Gets the base configuration file, or <see langword="null"/> for none.
    /// </summary>
    public string? BaseConfigFile { get; }

    /// <summary>
    ///     Gets a value indicating whether the configuration and editor settings are discovered per file.
    /// </summary>
    public bool DiscoverPerFile { get; }

    /// <summary>
    ///     Creates the set that uses the configuration and editor settings that apply to each file.
    /// </summary>
    /// <returns>The set.</returns>
    public static OptionSet RepoConfiguration() => new("repo-config", [], null, discoverPerFile: true);

    /// <summary>
    ///     Creates the loader that supplies the configuration of the set.
    /// </summary>
    /// <returns>The loader.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the base file cannot be read or the settings are invalid.</exception>
    internal ConfigLoader CreateLoader()
    {
        // Discovery and the plain defaults need no file
        if (DiscoverPerFile)
        {
            return ConfigLoader.ForDiscovery();
        }

        if (BaseConfigFile is null && Values.Count == 0)
        {
            return ConfigLoader.ForDefaults();
        }

        // Merge the base file and the settings into one document in a private temporary directory, load it
        // once (the loader reads the file immediately) and remove it again
        var directory = Path.Combine(Path.GetTempPath(), "lintmark-verify-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, ConfigLoader.FileName);
            File.WriteAllText(path, Compose());
            var loader = ConfigLoader.ForFile(path);
            if (!loader.InitialErrors.IsDefaultOrEmpty)
            {
                var errors = string.Join("; ", loader.InitialErrors.Select(static e => e.Message));
                throw new InvalidOperationException($"The settings of '{Label}' are invalid: {errors}");
            }

            return loader;
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    ///     Builds the YAML document of the set.
    /// </summary>
    /// <returns>The base file overlaid with the settings.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the base file cannot be read or is not a mapping.</exception>
    internal string Compose()
    {
        // Start from the base file's top-level mapping, when there is one
        var root = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (BaseConfigFile is not null)
        {
            try
            {
                var parsed = new DeserializerBuilder().Build().Deserialize<Dictionary<object, object?>?>(File.ReadAllText(BaseConfigFile));
                foreach (var (key, value) in parsed ?? [])
                {
                    root[KeyText(key)] = value;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or YamlDotNet.Core.YamlException)
            {
                throw new InvalidOperationException($"Cannot use the configuration file '{BaseConfigFile}': {ex.Message}", ex);
            }
        }

        // Overlay the settings; a dotted key addresses a nested mapping
        foreach (var (key, value) in Values)
        {
            Set(root, key.Split('.'), 0, ToScalar(value));
        }

        return new SerializerBuilder().Build().Serialize(root);
    }

    /// <summary>
    ///     Sets a value in a nested mapping, replacing anything that is not a mapping on the way.
    /// </summary>
    /// <param name="mapping">The mapping to change.</param>
    /// <param name="path">The key path.</param>
    /// <param name="index">The position in the path.</param>
    /// <param name="value">The value to set.</param>
    private static void Set(Dictionary<string, object?> mapping, string[] path, int index, object value)
    {
        if (index == path.Length - 1)
        {
            mapping[path[index]] = value;
            return;
        }

        // An existing nested mapping is copied to string keys so the overlay can address it
        var child = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (mapping.TryGetValue(path[index], out var existing) && existing is Dictionary<object, object?> nested)
        {
            foreach (var (key, nestedValue) in nested)
            {
                child[KeyText(key)] = nestedValue;
            }
        }
        else if (mapping.TryGetValue(path[index], out var same) && same is Dictionary<string, object?> typed)
        {
            child = typed;
        }

        mapping[path[index]] = child;
        Set(child, path, index + 1, value);
    }

    /// <summary>
    ///     Converts the text of a setting into the YAML scalar it stands for.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>A boolean, an integer or the text itself.</returns>
    private static object ToScalar(string value)
    {
        if (bool.TryParse(value, out var flag))
        {
            return flag;
        }

        return long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ? number : value;
    }

    /// <summary>
    ///     Converts a mapping key to text.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The text.</returns>
    private static string KeyText(object key) => Convert.ToString(key, CultureInfo.InvariantCulture) ?? string.Empty;
}
