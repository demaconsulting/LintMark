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

using DemaConsulting.LintMark.Verify.Cli;

namespace DemaConsulting.LintMark.Verify;

/// <summary>
///     Entry point of the verification tool.
/// </summary>
internal static class Program
{
    /// <summary>
    ///     Runs the verifier with the command line.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>0 when every check passed, 1 when a check failed, 2 for a usage or setup error.</returns>
    public static async Task<int> Main(string[] args)
    {
        VerifyOptions options;
        try
        {
            options = VerifyOptions.Parse(args);
        }
        catch (UsageException ex)
        {
            await Console.Error.WriteLineAsync($"error: {ex.Message}");
            await Console.Error.WriteLineAsync("Use --help for the usage.");
            return VerifyApplication.UsageError;
        }

        return await VerifyApplication.RunAsync(options, Console.Out, Console.Error, Directory.GetCurrentDirectory());
    }
}
