using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using Env = System.Environment;

namespace LBT.Toolchain;

/// <summary>
/// MSVC toolchain (Visual Studio C++ compiler).
/// </summary>
public partial class MsvcToolchain : Toolchain
{
    /// <summary>
    /// Gets the toolchain type.
    /// </summary>
    public override ToolchainType Type => ToolchainType.MSVC;

    /// <summary>
    /// Gets the toolchain name.
    /// </summary>
    public override string Name => "Microsoft Visual C++";

    private string? _vcvarsPath;
    private Dictionary<string, string?>? _buildEnvironment;

    /// <summary>
    /// MSVC build environment variables (includes PATH, INCLUDE, LIB, etc.).
    /// Available after calling InitializeEnvironmentAsync().
    /// </summary>
    public override IReadOnlyDictionary<string, string?>? BuildEnvironment => _buildEnvironment;

    /// <inheritdoc />
    public override async Task<bool> DetectAsync()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        // Try to find vswhere.exe
        var vswherePath = Path.Combine(
            Env.GetFolderPath(Env.SpecialFolder.ProgramFilesX86),
            "Microsoft Visual Studio", "Installer", "vswhere.exe"
        );

        if (!File.Exists(vswherePath))
            return false;

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = vswherePath,
                Arguments = "-latest -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = Process.Start(psi);
            if (process == null) return false;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            var vsPath = output.Trim();
            if (string.IsNullOrEmpty(vsPath) || !Directory.Exists(vsPath))
                return false;

            // Find vcvarsall.bat
            _vcvarsPath = Path.Combine(vsPath, "VC", "Auxiliary", "Build", "vcvarsall.bat");
            if (!File.Exists(_vcvarsPath))
                return false;

            // Find cl.exe
            var vcToolsPath = Path.Combine(vsPath, "VC", "Tools", "MSVC");
            if (!Directory.Exists(vcToolsPath))
                return false;

            var latestVersion = Directory.GetDirectories(vcToolsPath)
                .Select(d => new DirectoryInfo(d).Name)
                .OrderByDescending(v => v)
                .FirstOrDefault();

            if (latestVersion == null)
                return false;

            Version = latestVersion;
            var hostArch = Env.Is64BitOperatingSystem ? "Hostx64" : "Hostx86";
            var targetArch = "x64";

            CompilerPath = Path.Combine(vcToolsPath, latestVersion, "bin", hostArch, targetArch, "cl.exe");
            LinkerPath = Path.Combine(vcToolsPath, latestVersion, "bin", hostArch, targetArch, "link.exe");
            ArchiverPath = Path.Combine(vcToolsPath, latestVersion, "bin", hostArch, targetArch, "lib.exe");

            return File.Exists(CompilerPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Initializes MSVC build environment variables
    /// By executing vcvarsall.bat to get INCLUDE, LIB, PATH and other variables
    /// </summary>
    /// <returns>True if successful; otherwise, false.</returns>
    public override async Task<bool> InitializeEnvironmentAsync()
    {
        return await InitializeEnvironmentAsync("x64");
    }

    /// <summary>
    /// Initializes MSVC build environment variables (with specified architecture)
    /// By executing vcvarsall.bat to get INCLUDE, LIB, PATH and other variables
    /// </summary>
    /// <param name="arch">Target architecture: x64, x86, arm, arm64</param>
    /// <returns>True if successful; otherwise, false.</returns>
    public async Task<bool> InitializeEnvironmentAsync(string arch)
    {
        if (_buildEnvironment != null)
            return true; // Already initialized

        if (string.IsNullOrEmpty(_vcvarsPath) || !File.Exists(_vcvarsPath))
            return false;

        try
        {
            var environment = await MsvcEnvironment.GetEnvironmentAsync(_vcvarsPath, arch);
            if (environment == null || environment.Count == 0)
                return false;

            _buildEnvironment = environment;
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc />
    public override CompileCommand GetCompileCommand(CompileOptions options)
    {
        var args = new List<string>
        {
            "/nologo",
            "/c",  // Compile only, do not link
            "/EHsc",  // Exception handling
            "/utf-8",  // Source file encoding
        };

        // C++ language standard
        var cppStd = BuildSystem.CppStandard;
        if (cppStd != CppStandard.Default)
        {
            args.Add(cppStd switch
            {
                CppStandard.Cpp11 => "/std:c++11",
                CppStandard.Cpp14 => "/std:c++14",
                CppStandard.Cpp17 => "/std:c++17",
                CppStandard.Cpp20 => "/std:c++20",
                CppStandard.Cpp23 => "/std:c++latest",
                _ => ""
            });
        }

        // C language standard
        var cStd = BuildSystem.CStandard;
        if (cStd != CStandard.Default)
        {
            args.Add(cStd switch
            {
                CStandard.C11 => "/std:c11",
                CStandard.C17 => "/std:c17",
                _ => ""
            });
        }

        // Configuration related
        switch (options.Configuration)
        {
            case BuildConfiguration.Debug:
                args.AddRange(new[] { "/Od", "/Zi", "/MDd", "/RTC1" });
                args.Add("/D_DEBUG");
                break;
            case BuildConfiguration.Release:
                args.AddRange(new[] { "/O2", "/MD", "/DNDEBUG" });
                break;
            case BuildConfiguration.RelWithDebInfo:
                args.AddRange(new[] { "/O2", "/Zi", "/MD", "/DNDEBUG" });
                break;
            case BuildConfiguration.MinSizeRel:
                args.AddRange(new[] { "/O1", "/MD", "/DNDEBUG" });
                break;
        }

        // Global preprocessor definitions
        foreach (var define in BuildSystem.GlobalDefines)
        {
            args.Add($"/D{define}");
        }

        // Include directories (CliWrap handles spaces in paths automatically)
        foreach (var dir in options.IncludeDirs)
        {
            args.Add($"/I{dir}");
        }

        // Preprocessor definitions
        foreach (var define in options.Defines)
        {
            args.Add($"/D{define}");
        }

        // Dependency tracking
        if (options.GenerateDependencies)
        {
            args.Add("/showIncludes");
            // Force English output for consistent parsing
            args.Add("/English-");
        }

        // Output file
        args.Add($"/Fo{options.OutputFile}");

        // Debug information file
        if (options.Configuration is BuildConfiguration.Debug or BuildConfiguration.RelWithDebInfo)
        {
            var pdbPath = Path.ChangeExtension(options.OutputFile, ".pdb");
            args.Add($"/Fd{pdbPath}");
        }

        // Source file
        args.Add(options.SourceFile);

        // Extra flags
        args.AddRange(options.ExtraFlags);

        return new CompileCommand
        {
            Executable = CompilerPath,
            Arguments = args
        };
    }

    /// <inheritdoc />
    public override LinkCommand GetLinkCommand(LinkOptions options)
    {
        var args = new List<string> { "/nologo" };

        // Output type
        switch (options.OutputType)
        {
            case ProjectType.Executable:
                args.Add("/SUBSYSTEM:CONSOLE");
                break;
            case ProjectType.SharedLibrary:
                args.Add("/DLL");
                break;
            case ProjectType.StaticLibrary:
                // Use lib.exe instead of link.exe
                return GetArchiveCommand(options);
        }

        // Configuration related
        if (options.Configuration == BuildConfiguration.Debug)
        {
            args.Add("/DEBUG");
        }

        // Output file
        args.Add($"/OUT:{options.OutputFile}");

        // Library directories
        foreach (var dir in options.LibraryDirs)
        {
            args.Add($"/LIBPATH:{dir}");
        }

        // Link libraries
        foreach (var lib in options.Libraries)
        {
            // If this is a project reference, find the corresponding .lib file
            if (!lib.EndsWith(".lib", StringComparison.OrdinalIgnoreCase))
            {
                args.Add($"{lib}.lib");
            }
            else
            {
                args.Add(lib);
            }
        }

        // Object files
        foreach (var obj in options.ObjectFiles)
        {
            args.Add(obj);
        }

        // Extra flags
        args.AddRange(options.ExtraFlags);

        return new LinkCommand
        {
            Executable = LinkerPath,
            Arguments = args
        };
    }

    private LinkCommand GetArchiveCommand(LinkOptions options)
    {
        var args = new List<string>
        {
            "/nologo",
            $"/OUT:{options.OutputFile}"
        };

        foreach (var obj in options.ObjectFiles)
        {
            args.Add(obj);
        }

        return new LinkCommand
        {
            Executable = ArchiverPath,
            Arguments = args
        };
    }
}
