using System.Diagnostics;

namespace LBT.Toolchain;

/// <summary>
/// Clang toolchain.
/// </summary>
public class ClangToolchain : Toolchain
{
    /// <summary>
    /// Gets the toolchain type.
    /// </summary>
    public override ToolchainType Type => ToolchainType.Clang;
    /// <summary>
    /// Gets the toolchain name.
    /// </summary>
    public override string Name => "Clang/LLVM";

    /// <inheritdoc />
    public override async Task<bool> DetectAsync()
    {
        // Try common clang paths
        var candidates = new List<string>();

        if (OperatingSystem.IsWindows())
        {
            // LLVM installation path
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            candidates.Add(Path.Combine(programFiles, "LLVM", "bin", "clang++.exe"));

            // Clang bundled with Visual Studio
            var vsPaths = new[] { "2022", "2019" };
            foreach (var vs in vsPaths)
            {
                candidates.Add(Path.Combine(programFiles, "Microsoft Visual Studio", vs, "Enterprise", "VC", "Tools", "Llvm", "x64", "bin", "clang++.exe"));
                candidates.Add(Path.Combine(programFiles, "Microsoft Visual Studio", vs, "Professional", "VC", "Tools", "Llvm", "x64", "bin", "clang++.exe"));
                candidates.Add(Path.Combine(programFiles, "Microsoft Visual Studio", vs, "Community", "VC", "Tools", "Llvm", "x64", "bin", "clang++.exe"));
            }

            candidates.Add("clang++.exe");
        }
        else
        {
            candidates.AddRange(new[]
            {
                "/usr/bin/clang++",
                "/usr/local/bin/clang++",
                "/opt/homebrew/bin/clang++",
                "clang++"
            });
        }

        foreach (var candidate in candidates)
        {
            if (await TryDetectAsync(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> TryDetectAsync(string path)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var process = Process.Start(psi);
            if (process == null) return false;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0) return false;

            // Parse version
            var match = System.Text.RegularExpressions.Regex.Match(output, @"version (\d+\.\d+\.\d+)");
            if (match.Success)
            {
                Version = match.Groups[1].Value;
            }

            CompilerPath = path;
            LinkerPath = path; // clang++ can also be used for linking
            ArchiverPath = OperatingSystem.IsWindows() ? "llvm-ar.exe" : "ar";

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
            "-c",  // Compile only, no linking
        };

        // C++ language standard
        var cppStd = BuildSystem.CppStandard;
        args.Add(cppStd switch
        {
            CppStandard.Cpp11 => "-std=c++11",
            CppStandard.Cpp14 => "-std=c++14",
            CppStandard.Cpp17 => "-std=c++17",
            CppStandard.Cpp20 => "-std=c++20",
            CppStandard.Cpp23 => "-std=c++23",
            _ => "-std=c++17"  // Default to C++17
        });

        // Configuration-specific flags
        switch (options.Configuration)
        {
            case BuildConfiguration.Debug:
                args.AddRange(new[] { "-O0", "-g", "-D_DEBUG" });
                break;
            case BuildConfiguration.Release:
                args.AddRange(new[] { "-O3", "-DNDEBUG" });
                break;
            case BuildConfiguration.RelWithDebInfo:
                args.AddRange(new[] { "-O2", "-g", "-DNDEBUG" });
                break;
            case BuildConfiguration.MinSizeRel:
                args.AddRange(new[] { "-Os", "-DNDEBUG" });
                break;
        }

        // Global preprocessor definitions
        foreach (var define in BuildSystem.GlobalDefines)
        {
            args.Add($"-D{define}");
        }

        // Header file directories
        foreach (var dir in options.IncludeDirs)
        {
            args.Add($"-I{dir}");
        }

        // Preprocessor definitions
        foreach (var define in options.Defines)
        {
            args.Add($"-D{define}");
        }

        // Dependency tracking
        if (options.GenerateDependencies && options.DependencyFile != null)
        {
            args.Add("-MMD");
            args.Add($"-MF{options.DependencyFile}");
        }

        // Output file
        args.Add("-o");
        args.Add(options.OutputFile);

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
        if (options.OutputType == ProjectType.StaticLibrary)
        {
            return GetArchiveCommand(options);
        }

        var args = new List<string>();

        // Shared library
        if (options.OutputType == ProjectType.SharedLibrary)
        {
            args.Add("-shared");
            if (!OperatingSystem.IsWindows())
            {
                args.Add("-fPIC");
            }
        }

        // Configuration-specific flags
        if (options.Configuration == BuildConfiguration.Debug)
        {
            args.Add("-g");
        }

        // Object files
        args.AddRange(options.ObjectFiles);

        // Library directories
        foreach (var dir in options.LibraryDirs)
        {
            args.Add($"-L{dir}");
        }

        // Link libraries
        foreach (var lib in options.Libraries)
        {
            args.Add($"-l{lib}");
        }

        // Output file
        args.Add("-o");
        args.Add(options.OutputFile);

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
            "rcs",
            options.OutputFile
        };
        args.AddRange(options.ObjectFiles);

        return new LinkCommand
        {
            Executable = ArchiverPath,
            Arguments = args
        };
    }
}
