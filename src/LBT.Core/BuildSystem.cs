namespace LBT;

using LBT.Toolchain;

/// <summary>
/// Target architecture.
/// </summary>
public enum Architecture
{
    /// <summary>x86 (32-bit)</summary>
    X86,
    /// <summary>x64 (64-bit)</summary>
    X64,
    /// <summary>ARM (32-bit)</summary>
    Arm,
    /// <summary>ARM64 (64-bit)</summary>
    Arm64,
    /// <summary>Universal (for macOS)</summary>
    Universal
}

/// <summary>
/// Target platform.
/// </summary>
public enum Platform
{
    /// <summary>Windows</summary>
    Windows,
    /// <summary>Linux</summary>
    Linux,
    /// <summary>macOS</summary>
    MacOS,
    /// <summary>iOS</summary>
    IOS,
    /// <summary>Android</summary>
    Android
}

/// <summary>
/// Build rule.
/// </summary>
public enum BuildRule
{
    /// <summary>Debug mode</summary>
    ModeDebug,
    /// <summary>Release mode</summary>
    ModeRelease
}

/// <summary>
/// C++ language standard.
/// </summary>
public enum CppStandard
{
    /// <summary>C++11</summary>
    Cpp11,
    /// <summary>C++14</summary>
    Cpp14,
    /// <summary>C++17</summary>
    Cpp17,
    /// <summary>C++20</summary>
    Cpp20,
    /// <summary>C++23</summary>
    Cpp23,
    /// <summary>Use compiler default</summary>
    Default
}

/// <summary>
/// C language standard.
/// </summary>
public enum CStandard
{
    /// <summary>C99</summary>
    C99,
    /// <summary>C11</summary>
    C11,
    /// <summary>C17</summary>
    C17,
    /// <summary>C23</summary>
    C23,
    /// <summary>Use compiler default</summary>
    Default
}

/// <summary>
/// Global entry point for the build system.
/// </summary>
public static class BuildSystem
{
    private static readonly Dictionary<string, Project> _projects = new();
    private static readonly List<string> _includedPaths = new();
    private static string _rootDirectory = Environment.CurrentDirectory;

    // Global settings
    private static string _projectName = "Untitled";
    private static string _version = "1.0.0";
    private static CppStandard _cppStandard = CppStandard.Default;
    private static CStandard _cStandard = CStandard.Default;
    private static readonly List<string> _globalDefines = new();
    private static Architecture _architecture = Architecture.X64;
    private static ToolchainType _toolchain = ToolchainType.MSVC;
    private static Platform _platform = Platform.Windows;
    private static readonly List<BuildRule> _rules = new();
    private static BuildConfiguration _currentMode = BuildConfiguration.Debug;
    private static string _currentModeString = "debug";

    #region Constants (for direct use in scripts)

    /// <summary>x86 architecture</summary>
    public const Architecture X86 = Architecture.X86;
    /// <summary>x64 architecture</summary>
    public const Architecture X64 = Architecture.X64;
    /// <summary>ARM architecture</summary>
    public const Architecture Arm = Architecture.Arm;
    /// <summary>ARM64 architecture</summary>
    public const Architecture Arm64 = Architecture.Arm64;

    /// <summary>Windows platform</summary>
    public const Platform Windows = Platform.Windows;
    /// <summary>Linux platform</summary>
    public const Platform Linux = Platform.Linux;
    /// <summary>macOS platform</summary>
    public const Platform MacOS = Platform.MacOS;
    /// <summary>iOS platform</summary>
    public const Platform IOS = Platform.IOS;
    /// <summary>Android platform</summary>
    public const Platform Android = Platform.Android;

    /// <summary>C++11 standard</summary>
    public const CppStandard Cpp11 = CppStandard.Cpp11;
    /// <summary>C++14 standard</summary>
    public const CppStandard Cpp14 = CppStandard.Cpp14;
    /// <summary>C++17 standard</summary>
    public const CppStandard Cpp17 = CppStandard.Cpp17;
    /// <summary>C++20 standard</summary>
    public const CppStandard Cpp20 = CppStandard.Cpp20;
    /// <summary>C++23 standard</summary>
    public const CppStandard Cpp23 = CppStandard.Cpp23;

    /// <summary>C99 standard</summary>
    public const CStandard C99 = CStandard.C99;
    /// <summary>C11 standard</summary>
    public const CStandard C11 = CStandard.C11;
    /// <summary>C17 standard</summary>
    public const CStandard C17 = CStandard.C17;
    /// <summary>C23 standard</summary>
    public const CStandard C23 = CStandard.C23;

    /// <summary>Executable file</summary>
    public const ProjectType Executable = ProjectType.Executable;
    /// <summary>Static library</summary>
    public const ProjectType StaticLibrary = ProjectType.StaticLibrary;
    /// <summary>Shared library</summary>
    public const ProjectType SharedLibrary = ProjectType.SharedLibrary;

    /// <summary>Debug build rule</summary>
    public const BuildRule ModeDebug = BuildRule.ModeDebug;
    /// <summary>Release build rule</summary>
    public const BuildRule ModeRelease = BuildRule.ModeRelease;

    #endregion

    #region Global properties

    /// <summary>
    /// Project name.
    /// </summary>
    public static string ProjectName => _projectName;

    /// <summary>
    /// Project version.
    /// </summary>
    public static string Version => _version;

    /// <summary>
    /// Global C++ standard.
    /// </summary>
    public static CppStandard CppStandard => _cppStandard;

    /// <summary>
    /// Global C standard.
    /// </summary>
    public static CStandard CStandard => _cStandard;

    /// <summary>
    /// Global preprocessor definitions.
    /// </summary>
    public static IReadOnlyList<string> GlobalDefines => _globalDefines;

    /// <summary>
    /// Target architecture.
    /// </summary>
    public static Architecture Architecture => _architecture;

    /// <summary>
    /// Compiler toolchain.
    /// </summary>
    public static ToolchainType Toolchain => _toolchain;

    /// <summary>
    /// Target platform.
    /// </summary>
    public static Platform Platform => _platform;

    /// <summary>
    /// Build rules.
    /// </summary>
    public static IReadOnlyList<BuildRule> Rules => _rules;

    /// <summary>
    /// Current build mode.
    /// </summary>
    public static BuildConfiguration CurrentMode => _currentMode;

    /// <summary>
    /// Current build mode as a string (supports custom modes).
    /// </summary>
    public static string CurrentModeString => _currentModeString;

    #endregion

    #region Global setting methods (similar to xmake's set_xxx)

    /// <summary>
    /// Sets the project name.
    /// </summary>
    /// <param name="name">The project name.</param>
    public static void SetProject(string name)
    {
        _projectName = name;
    }

    /// <summary>
    /// Sets the project version.
    /// </summary>
    /// <param name="version">The version string.</param>
    public static void SetVersion(string version)
    {
        _version = version;
    }

    /// <summary>
    /// Sets the C++ language standard.
    /// </summary>
    /// <param name="cppStandard">The C++ standard to use.</param>
    public static void SetLanguages(CppStandard cppStandard)
    {
        _cppStandard = cppStandard;
    }

    /// <summary>
    /// Sets the C language standard.
    /// </summary>
    /// <param name="cStandard">The C standard to use.</param>
    public static void SetLanguages(CStandard cStandard)
    {
        _cStandard = cStandard;
    }

    /// <summary>
    /// Sets both C and C++ language standards.
    /// </summary>
    /// <param name="cStandard">The C standard to use.</param>
    /// <param name="cppStandard">The C++ standard to use.</param>
    public static void SetLanguages(CStandard cStandard, CppStandard cppStandard)
    {
        _cStandard = cStandard;
        _cppStandard = cppStandard;
    }

    /// <summary>
    /// Sets language standard using string format (e.g., "c++17", "c11").
    /// </summary>
    /// <param name="standard">The standard as a string.</param>
    public static void SetLanguages(string standard)
    {
        var lower = standard.ToLowerInvariant().Replace(" ", "");
        _cppStandard = lower switch
        {
            "c++11" or "cxx11" => CppStandard.Cpp11,
            "c++14" or "cxx14" => CppStandard.Cpp14,
            "c++17" or "cxx17" => CppStandard.Cpp17,
            "c++20" or "cxx20" => CppStandard.Cpp20,
            "c++23" or "cxx23" => CppStandard.Cpp23,
            _ => _cppStandard
        };
        _cStandard = lower switch
        {
            "c99" => CStandard.C99,
            "c11" => CStandard.C11,
            "c17" => CStandard.C17,
            "c23" => CStandard.C23,
            _ => _cStandard
        };
    }

    /// <summary>
    /// Adds global preprocessor definitions.
    /// </summary>
    /// <param name="defines">The preprocessor definitions to add.</param>
    public static void AddDefines(params string[] defines)
    {
        _globalDefines.AddRange(defines);
    }

    /// <summary>
    /// Sets the target architecture.
    /// </summary>
    /// <param name="arch">The architecture.</param>
    public static void SetArch(Architecture arch)
    {
        _architecture = arch;
    }

    /// <summary>
    /// Sets the target architecture using string format.
    /// </summary>
    /// <param name="arch">The architecture as a string (e.g., "x64", "x86", "arm", "arm64").</param>
    public static void SetArch(string arch)
    {
        _architecture = arch.ToLowerInvariant() switch
        {
            "x86" => Architecture.X86,
            "x64" => Architecture.X64,
            "arm" => Architecture.Arm,
            "arm64" => Architecture.Arm64,
            "universal" => Architecture.Universal,
            _ => throw new ArgumentException($"Unknown architecture: {arch}")
        };
    }

    /// <summary>
    /// Sets the compiler toolchain.
    /// </summary>
    /// <param name="toolchain">The toolchain.</param>
    public static void SetToolchains(ToolchainType toolchain)
    {
        _toolchain = toolchain;
    }

    /// <summary>
    /// Sets the compiler toolchain using string format.
    /// </summary>
    /// <param name="toolchain">The toolchain as a string (e.g., "msvc", "gcc", "clang").</param>
    public static void SetToolchains(string toolchain)
    {
        _toolchain = toolchain.ToLowerInvariant() switch
        {
            "msvc" => ToolchainType.MSVC,
            "gcc" => ToolchainType.GCC,
            "clang" => ToolchainType.Clang,
            _ => throw new ArgumentException($"Unknown toolchain: {toolchain}")
        };
    }

    /// <summary>
    /// Sets the target platform.
    /// </summary>
    /// <param name="platform">The platform.</param>
    public static void SetPlat(Platform platform)
    {
        _platform = platform;
    }

    /// <summary>
    /// Sets the target platform using string format.
    /// </summary>
    /// <param name="platform">The platform as a string (e.g., "windows", "linux", "macos").</param>
    public static void SetPlat(string platform)
    {
        _platform = platform.ToLowerInvariant() switch
        {
            "windows" => Platform.Windows,
            "linux" => Platform.Linux,
            "macos" => Platform.MacOS,
            "ios" => Platform.IOS,
            "android" => Platform.Android,
            _ => throw new ArgumentException($"Unknown platform: {platform}")
        };
    }

    /// <summary>
    /// Adds build rules.
    /// </summary>
    /// <param name="rules">The rules to add.</param>
    public static void AddRules(params BuildRule[] rules)
    {
        _rules.AddRange(rules);
    }

    /// <summary>
    /// Adds build rules using string format.
    /// </summary>
    /// <param name="rules">The rules as strings (e.g., "mode.debug", "mode.release").</param>
    public static void AddRules(params string[] rules)
    {
        foreach (var rule in rules)
        {
            var lower = rule.ToLowerInvariant().Replace(".", "").Replace("_", "");
            _rules.Add(lower switch
            {
                "modedebug" => BuildRule.ModeDebug,
                "moderelease" => BuildRule.ModeRelease,
                _ => throw new ArgumentException($"Unknown rule: {rule}")
            });
        }
    }

    /// <summary>
    /// Checks if the current build mode matches the given mode.
    /// Supports custom modes (case-insensitive string comparison).
    /// </summary>
    /// <param name="mode">The mode to check (e.g., "Debug", "Release", or custom modes).</param>
    /// <returns>True if the current mode matches.</returns>
    public static bool IsMode(string mode)
    {
        return string.Equals(_currentModeString, mode, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if the current build mode matches the given mode.
    /// </summary>
    /// <param name="mode">The mode to check.</param>
    /// <returns>True if the current mode matches.</returns>
    public static bool IsMode(BuildConfiguration mode)
    {
        return _currentMode == mode;
    }

    /// <summary>
    /// Sets the current build mode.
    /// </summary>
    /// <param name="mode">The mode to set.</param>
    public static void SetMode(BuildConfiguration mode)
    {
        _currentMode = mode;
        _currentModeString = mode.ToString();
    }

    /// <summary>
    /// Sets the current build mode using string (supports custom modes).
    /// </summary>
    /// <param name="mode">The mode to set (e.g., "debug", "release", or custom modes).</param>
    public static void SetMode(string mode)
    {
        _currentModeString = mode;
        // Try to parse as standard BuildConfiguration
        if (Enum.TryParse<BuildConfiguration>(mode, true, out var config))
        {
            _currentMode = config;
        }
    }

    #endregion

    /// <summary>
    /// All registered projects.
    /// </summary>
    public static IReadOnlyDictionary<string, Project> Projects => _projects;

    /// <summary>
    /// Project root directory.
    /// </summary>
    public static string RootDirectory
    {
        get => _rootDirectory;
        set => _rootDirectory = Path.GetFullPath(value);
    }

    /// <summary>
    /// Registers a project.
    /// </summary>
    /// <param name="project">The project to register.</param>
    internal static void RegisterProject(Project project)
    {
        _projects[project.Name] = project;
    }

    /// <summary>
    /// Gets a project by name.
    /// </summary>
    /// <param name="name">The project name.</param>
    /// <returns>The project, or null if not found.</returns>
    public static Project? GetProject(string name)
    {
        return _projects.TryGetValue(name, out var project) ? project : null;
    }

    /// <summary>
    /// Includes a sub-module's build.cs file.
    /// </summary>
    /// <param name="relativePath">The path relative to the project root directory.</param>
    public static void Include(string relativePath)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootDirectory, relativePath));
        var buildFile = Path.Combine(fullPath, "build.cs");

        if (!File.Exists(buildFile))
        {
            throw new FileNotFoundException($"Cannot find build script: {buildFile}");
        }

        _includedPaths.Add(buildFile);
    }

    /// <summary>
    /// Gets all script paths to be loaded.
    /// </summary>
    /// <returns>A read-only list of script paths.</returns>
    public static IReadOnlyList<string> GetIncludedPaths() => _includedPaths;

    /// <summary>
    /// Starts the build process.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Exit code (0 for success, non-zero for failure).</returns>
    public static async Task<int> RunAsync(string[] args)
    {
        var engine = new BuildEngine();
        return await engine.ExecuteAsync(args);
    }

    /// <summary>
    /// Synchronous version of the build entry point.
    /// </summary>
    /// <param name="args">Command line arguments (optional).</param>
    /// <returns>Exit code (0 for success, non-zero for failure).</returns>
    public static int Run(string[]? args = null)
    {
        return RunAsync(args ?? Array.Empty<string>()).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Resets the build system state (mainly for testing).
    /// </summary>
    public static void Reset()
    {
        _projects.Clear();
        _includedPaths.Clear();
        _rootDirectory = Environment.CurrentDirectory;
        _projectName = "Untitled";
        _version = "1.0.0";
        _cppStandard = CppStandard.Default;
        _cStandard = CStandard.Default;
        _globalDefines.Clear();
    }
}
