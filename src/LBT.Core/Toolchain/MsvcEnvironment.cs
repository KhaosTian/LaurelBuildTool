using System.Diagnostics;
using System.Text;

namespace LBT.Toolchain;

/// <summary>
/// MSVC development environment variable manager.
/// Responsible for obtaining environment variables required for compilation from vcvarsall.bat.
/// </summary>
public class MsvcEnvironment
{
    private static readonly object _lock = new();
    private static Dictionary<string, string?>? _cachedEnvironment;
    private static string? _cachedArch;

    /// <summary>
    /// Gets the MSVC build environment variables.
    /// </summary>
    /// <param name="vcvarsPath">The path to vcvarsall.bat.</param>
    /// <param name="arch">The target architecture (x64, x86, arm64).</param>
    /// <returns>The environment variable dictionary.</returns>
    public static async Task<Dictionary<string, string?>?> GetEnvironmentAsync(
        string vcvarsPath,
        string arch = "x64")
    {
        // Use cache
        lock (_lock)
        {
            if (_cachedEnvironment != null && _cachedArch == arch)
            {
                return _cachedEnvironment;
            }
        }

        if (!File.Exists(vcvarsPath))
        {
            return null;
        }

        try
        {
            // Create a temporary batch file to retrieve environment variables
            // Principle: After running vcvarsall.bat, use the SET command to output all environment variables
            var tempBat = Path.GetTempFileName() + ".bat";
            var script = $"""
                @echo off
                call "{vcvarsPath}" {arch} >nul 2>&1
                if errorlevel 1 exit /b 1
                set
                """;

            await File.WriteAllTextAsync(tempBat, script, Encoding.Default);

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{tempBat}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.Default
                };

                var process = Process.Start(psi);
                if (process == null) return null;

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    return null;
                }

                // Parse environment variables
                var env = ParseEnvironmentVariables(output);

                // Cache the result
                lock (_lock)
                {
                    _cachedEnvironment = env;
                    _cachedArch = arch;
                }

                return env;
            }
            finally
            {
                // Clean up temporary file
                try { File.Delete(tempBat); } catch { }
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses the output of the SET command into an environment variable dictionary.
    /// </summary>
    /// <param name="output">The SET command output.</param>
    /// <returns>The environment variable dictionary.</returns>
    private static Dictionary<string, string?> ParseEnvironmentVariables(string output)
    {
        var env = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex > 0)
            {
                var key = trimmed[..eqIndex];
                var value = trimmed[(eqIndex + 1)..];
                env[key] = value;
            }
        }

        return env;
    }

    /// <summary>
    /// Clears the cache (for testing or switching architectures).
    /// </summary>
    public static void ClearCache()
    {
        lock (_lock)
        {
            _cachedEnvironment = null;
            _cachedArch = null;
        }
    }

    /// <summary>
    /// Gets the key environment variables (for debugging).
    /// </summary>
    /// <returns>An array of key variable names.</returns>
    public static string[] GetKeyVariables()
    {
        return new[] { "PATH", "INCLUDE", "LIB", "LIBPATH", "WindowsSdkDir", "VCToolsInstallDir" };
    }
}
