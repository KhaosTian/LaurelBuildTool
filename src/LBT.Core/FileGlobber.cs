using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace LBT;

/// <summary>
/// File glob pattern matching utility.
/// </summary>
public static class FileGlobber
{
    /// <summary>
    /// Matches files using a glob pattern.
    /// </summary>
    /// <param name="baseDirectory">The base directory to search in.</param>
    /// <param name="pattern">The glob pattern, such as "*.cpp" or "**/*.h".</param>
    /// <returns>An enumerable collection of full file paths that match the pattern.</returns>
    public static IEnumerable<string> Glob(string baseDirectory, string pattern)
    {
        var matcher = new Matcher();

        // Handle exclusion patterns (starting with !)
        if (pattern.StartsWith('!'))
        {
            matcher.AddExclude(pattern[1..]);
            return Enumerable.Empty<string>();
        }

        matcher.AddInclude(pattern);

        var directoryInfo = new DirectoryInfoWrapper(new DirectoryInfo(baseDirectory));
        var result = matcher.Execute(directoryInfo);

        return result.Files.Select(f => Path.GetFullPath(Path.Combine(baseDirectory, f.Path)));
    }

    /// <summary>
    /// Matches files using multiple glob patterns.
    /// </summary>
    /// <param name="baseDirectory">The base directory to search in.</param>
    /// <param name="patterns">The glob patterns. Patterns starting with '!' are treated as exclusion patterns.</param>
    /// <returns>An enumerable collection of full file paths that match the patterns.</returns>
    public static IEnumerable<string> Glob(string baseDirectory, params string[] patterns)
    {
        var matcher = new Matcher();

        foreach (var pattern in patterns)
        {
            if (pattern.StartsWith('!'))
            {
                matcher.AddExclude(pattern[1..]);
            }
            else
            {
                matcher.AddInclude(pattern);
            }
        }

        var directoryInfo = new DirectoryInfoWrapper(new DirectoryInfo(baseDirectory));
        var result = matcher.Execute(directoryInfo);

        return result.Files.Select(f => Path.GetFullPath(Path.Combine(baseDirectory, f.Path)));
    }
}
