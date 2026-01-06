namespace LBT;

/// <summary>
/// Project type.
/// </summary>
public enum ProjectType
{
    /// <summary>
    /// Executable file.
    /// </summary>
    Executable,

    /// <summary>
    /// Static library (.lib / .a).
    /// </summary>
    StaticLibrary,

    /// <summary>
    /// Shared library (.dll / .so / .dylib).
    /// </summary>
    SharedLibrary,

    /// <summary>
    /// Header-only interface library (no compilation, only headers).
    /// </summary>
    Interface
}

/// <summary>
/// Build configuration.
/// </summary>
public enum BuildConfiguration
{
    /// <summary>
    /// Debug configuration with no optimizations and debug symbols.
    /// </summary>
    Debug,

    /// <summary>
    /// Release configuration with optimizations and no debug symbols.
    /// </summary>
    Release,

    /// <summary>
    /// Release configuration with optimizations and debug symbols.
    /// </summary>
    RelWithDebInfo,

    /// <summary>
    /// Release configuration optimized for minimal binary size.
    /// </summary>
    MinSizeRel
}

/// <summary>
/// Include directory visibility for dependent projects.
/// </summary>
public enum Visibility
{
    /// <summary>
    /// Private - only visible within this target.
    /// </summary>
    Private,

    /// <summary>
    /// Public - visible to dependent targets.
    /// </summary>
    Public
}

/// <summary>
/// Represents a C++ project.
/// </summary>
public class Project
{
    private readonly List<FileItem> _sourceFiles = new();
    private readonly List<FileItem> _headerFiles = new();
    private readonly List<string> _includeDirs = new();
    private readonly List<string> _exportIncludeDirs = new();
    private readonly List<string> _defines = new();
    private readonly List<string> _dependencies = new();  // Target dependencies
    private readonly List<string> _linkedLibraries = new();
    private readonly List<string> _systemLibraries = new();
    private readonly List<string> _linkDirs = new();
    private readonly List<string> _compilerFlags = new();
    private readonly List<string> _linkerFlags = new();
    private readonly List<string> _publicIncludeDirs = new();  // Explicitly public include dirs
    private string? _pchHeader;

    /// <summary>
    /// The project name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The project type.
    /// </summary>
    public ProjectType Type { get; private set; } = ProjectType.Executable;

    /// <summary>
    /// The project directory (where build.cs is located).
    /// </summary>
    public string Directory { get; internal set; } = Environment.CurrentDirectory;

    /// <summary>
    /// The output directory.
    /// Defaults to "build" for keeping build artifacts separate from source code.
    /// </summary>
    public string OutputDirectory { get; set; } = "build";

    /// <summary>
    /// The list of source files.
    /// </summary>
    public IReadOnlyList<FileItem> SourceFiles => _sourceFiles;

    /// <summary>
    /// The list of header files.
    /// </summary>
    public IReadOnlyList<FileItem> HeaderFiles => _headerFiles;

    /// <summary>
    /// The header file search directories.
    /// </summary>
    public IReadOnlyList<string> IncludeDirs => _includeDirs;

    /// <summary>
    /// The exported header directories (for use by other projects that depend on this one).
    /// Libraries (Static/Shared) auto-export their IncludeDirs.
    /// Interface targets require explicit ExportIncludeDir().
    /// </summary>
    public IReadOnlyList<string> ExportIncludeDirs => GetExportIncludeDirs();

    /// <summary>
    /// Gets all exported include directories based on project type and visibility.
    /// Rules:
    /// - Private IncludeDirs: only visible within this target (default)
    /// - Public IncludeDirs (via Visibility.Public): exported to dependents
    /// - Explicit ExportIncludeDir(): exported to dependents
    /// - Interface type: can only export via ExportIncludeDir()
    /// </summary>
    private List<string> GetExportIncludeDirs()
    {
        var result = new List<string>();

        // Add explicitly marked public include dirs
        result.AddRange(_publicIncludeDirs);

        // Add explicitly exported directories (for backward compatibility)
        result.AddRange(_exportIncludeDirs);

        return result;
    }

    /// <summary>
    /// The preprocessor definitions.
    /// </summary>
    public IReadOnlyList<string> Defines => _defines;

    /// <summary>
    /// The target dependencies (other targets this one depends on).
    /// </summary>
    public IReadOnlyList<string> Dependencies => _dependencies;

    /// <summary>
    /// Gets all dependencies (explicit Dependencies + LinkedLibraries for backward compatibility).
    /// </summary>
    /// <returns>Union of Dependencies and LinkedLibraries.</returns>
    internal IEnumerable<string> DistinctDependencies()
    {
        return _dependencies.Union(_linkedLibraries).Distinct();
    }

    /// <summary>
    /// The linked libraries.
    /// </summary>
    public IReadOnlyList<string> LinkedLibraries => _linkedLibraries;

    /// <summary>
    /// The system libraries.
    /// </summary>
    public IReadOnlyList<string> SystemLibraries => _systemLibraries;

    /// <summary>
    /// The library search directories.
    /// </summary>
    public IReadOnlyList<string> LinkDirs => _linkDirs;

    /// <summary>
    /// The precompiled header file.
    /// </summary>
    public string? PchHeader => _pchHeader;

    /// <summary>
    /// The compiler flags.
    /// </summary>
    public IReadOnlyList<string> CompilerFlags => _compilerFlags;

    /// <summary>
    /// The linker flags.
    /// </summary>
    public IReadOnlyList<string> LinkerFlags => _linkerFlags;

    private Project(string name)
    {
        Name = name;
        BuildSystem.RegisterProject(this);
    }

    /// <summary>
    /// Creates an executable project.
    /// </summary>
    /// <param name="name">The project name.</param>
    /// <returns>The created project.</returns>
    public static Project Create(string name)
    {
        return new Project(name) { Type = ProjectType.Executable };
    }

    /// <summary>
    /// Creates a target (similar to xmake's target() function).
    /// Defaults to executable type.
    /// </summary>
    /// <param name="name">The target name.</param>
    /// <returns>The created project.</returns>
    public static Project Target(string name)
    {
        return new Project(name) { Type = ProjectType.Executable };
    }

    /// <summary>
    /// Creates a static library project.
    /// </summary>
    /// <param name="name">The project name.</param>
    /// <returns>The created project.</returns>
    public static Project CreateStaticLibrary(string name)
    {
        return new Project(name) { Type = ProjectType.StaticLibrary };
    }

    /// <summary>
    /// Creates a shared library project.
    /// </summary>
    /// <param name="name">The project name.</param>
    /// <returns>The created project.</returns>
    public static Project CreateSharedLibrary(string name)
    {
        return new Project(name) { Type = ProjectType.SharedLibrary };
    }

    /// <summary>
    /// Sets the project type (similar to xmake's set_kind()).
    /// </summary>
    /// <param name="type">The project type.</param>
    /// <returns>This project instance for method chaining.</returns>
    public Project SetKind(ProjectType type)
    {
        Type = type;
        return this;
    }

    /// <summary>
    /// Sets the project type using string format (similar to xmake's set_kind()).
    /// </summary>
    /// <param name="kind">The kind as a string (e.g., "binary", "static", "shared").</param>
    /// <returns>This project instance for method chaining.</returns>
    public Project SetKind(string kind)
    {
        Type = kind.ToLowerInvariant() switch
        {
            "binary" => ProjectType.Executable,
            "static" => ProjectType.StaticLibrary,
            "shared" or "sharedlibrary" => ProjectType.SharedLibrary,
            "interface" or "headeronly" => ProjectType.Interface,
            "exe" or "executable" => ProjectType.Executable,
            _ => throw new ArgumentException($"Unknown project kind: {kind}")
        };
        return this;
    }

    /// <summary>
    /// Adds source files (supports glob patterns).
    /// </summary>
    /// <param name="patterns">The glob patterns for source files.</param>
    /// <returns>This project instance for method chaining.</returns>
    public Project AddFiles(params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var files = FileGlobber.Glob(Directory, pattern);
            foreach (var file in files)
            {
                _sourceFiles.Add(new FileItem(file, this));
            }
        }
        return this;
    }

    /// <summary>
    /// Adds header file search directories (private by default).
    /// </summary>
    /// <param name="dirs">The include directories.</param>
    /// <returns>This project instance for method chaining.</returns>
    public Project AddIncludeDir(params string[] dirs)
    {
        return AddIncludeDir(Visibility.Private, dirs);
    }

    /// <summary>
    /// Adds header file search directories with visibility control.
    /// </summary>
    /// <param name="visibility">The visibility (private or public).</param>
    /// <param name="dirs">The include directories.</param>
    /// <returns>This project instance for method chaining.</returns>
    public Project AddIncludeDir(Visibility visibility, params string[] dirs)
    {
        foreach (var dir in dirs)
        {
            var fullPath = Path.GetFullPath(Path.Combine(Directory, dir));
            _includeDirs.Add(fullPath);

            // If public, also add to public include dirs
            if (visibility == Visibility.Public)
            {
                _publicIncludeDirs.Add(fullPath);
            }
        }
        return this;
    }

    /// <summary>
    /// Exports header directories (for use by other projects).
    /// </summary>
    /// <param name="dirs">The directories to export.</param>
    /// <returns>This project instance for method chaining.</returns>
    public Project ExportIncludeDir(params string[] dirs)
    {
        foreach (var dir in dirs)
        {
            var fullPath = Path.GetFullPath(Path.Combine(Directory, dir));
            _exportIncludeDirs.Add(fullPath);
        }
        return this;
    }

    /// <summary>
    /// Adds preprocessor definitions.
    /// </summary>
    /// <param name="defines">The preprocessor definitions.</param>
    /// <returns>This project instance for method chaining.</returns>
    public Project AddDefine(params string[] defines)
    {
        _defines.AddRange(defines);
        return this;
    }

    /// <summary>
    /// Adds target dependencies (similar to xmake's add_deps()).
    /// Dependencies are used to:
    /// 1. Determine build order
    /// 2. Automatically inherit exported header directories
    /// 3. Link the dependency libraries if they are not interface targets
    /// </summary>
    /// <param name="deps">The target names to depend on.</param>
    /// <returns>This project instance for method chaining.</returns>
    public Project AddDeps(params string[] deps)
    {
        foreach (var dep in deps)
        {
            if (!string.IsNullOrEmpty(dep))
            {
                _dependencies.Add(dep);
            }
        }
        return this;
    }

    /// <summary>
    /// Links libraries (for external or pre-built libraries).
    /// For target dependencies, use AddDeps() instead.
    /// </summary>
    /// <param name="libraries">The libraries to link.</param>
    /// <returns>This project instance for method chaining.</returns>
    public Project Link(params string[] libraries)
    {
        _linkedLibraries.AddRange(libraries);
        return this;
    }

    /// <summary>
    /// Adds compiler flags.
    /// </summary>
    /// <param name="flags">The compiler flags.</param>
    /// <returns>This project instance for method chaining.</returns>
    public Project AddCompilerFlags(params string[] flags)
    {
        _compilerFlags.AddRange(flags);
        return this;
    }

    /// <summary>
    /// Adds linker flags.
    /// </summary>
    /// <param name="flags">The linker flags.</param>
    /// <returns>This project instance for method chaining.</returns>
    public Project AddLinkerFlags(params string[] flags)
    {
        _linkerFlags.AddRange(flags);
        return this;
    }

    /// <summary>
    /// Adds header files (supports glob patterns).
    /// </summary>
    /// <param name="patterns">The glob patterns for header files.</param>
    /// <returns>This project instance for method chaining.</returns>
    public Project AddHeaderFiles(params string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            var files = FileGlobber.Glob(Directory, pattern);
            foreach (var file in files)
            {
                _headerFiles.Add(new FileItem(file, this));
            }
        }
        return this;
    }

    /// <summary>
    /// Adds library search directories for linking.
    /// </summary>
    /// <param name="dirs">The library directories.</param>
    /// <returns>This project instance for method chaining.</returns>
    public Project AddLinkDir(params string[] dirs)
    {
        foreach (var dir in dirs)
        {
            var fullPath = Path.GetFullPath(Path.Combine(Directory, dir));
            _linkDirs.Add(fullPath);
        }
        return this;
    }

    /// <summary>
    /// Adds libraries to link.
    /// </summary>
    /// <param name="libraries">The libraries to link (e.g., "pthread", "ssl").</param>
    /// <returns>This project instance for method chaining.</returns>
    public Project AddLinks(params string[] libraries)
    {
        foreach (var lib in libraries)
        {
            if (!string.IsNullOrEmpty(lib))
            {
                _linkedLibraries.Add(lib);
            }
        }
        return this;
    }

    /// <summary>
    /// Adds system libraries to link.
    /// </summary>
    /// <param name="libraries">The system libraries to link (e.g., "pthread", "dl", "m").</param>
    /// <returns>This project instance for method chaining.</returns>
    public Project AddSysLinks(params string[] libraries)
    {
        foreach (var lib in libraries)
        {
            if (!string.IsNullOrEmpty(lib))
            {
                _systemLibraries.Add(lib);
            }
        }
        return this;
    }

    /// <summary>
    /// Sets the precompiled header file.
    /// </summary>
    /// <param name="headerPath">The path to the precompiled header file.</param>
    /// <returns>This project instance for method chaining.</returns>
    public Project SetPchHeader(string headerPath)
    {
        _pchHeader = headerPath;
        return this;
    }

    /// <summary>
    /// Sets the project type.
    /// </summary>
    /// <param name="type">The project type.</param>
    /// <returns>This project instance for method chaining.</returns>
    public Project SetType(ProjectType type)
    {
        Type = type;
        return this;
    }

    /// <summary>
    /// Sets the output directory.
    /// </summary>
    /// <param name="dir">The output directory path.</param>
    /// <returns>This project instance for method chaining.</returns>
    public Project SetOutputDir(string dir)
    {
        OutputDirectory = Path.GetFullPath(Path.Combine(Directory, dir));
        return this;
    }

    /// <summary>
    /// Gets the output file name based on project type and platform.
    /// </summary>
    /// <param name="config">The build configuration.</param>
    /// <returns>The output file name.</returns>
    public string GetOutputFileName(BuildConfiguration config = BuildConfiguration.Debug)
    {
        var suffix = config == BuildConfiguration.Debug ? "_d" : "";

        return Type switch
        {
            ProjectType.Executable => OperatingSystem.IsWindows()
                ? $"{Name}{suffix}.exe"
                : Name + suffix,

            ProjectType.StaticLibrary => OperatingSystem.IsWindows()
                ? $"{Name}{suffix}.lib"
                : $"lib{Name}{suffix}.a",

            ProjectType.SharedLibrary => OperatingSystem.IsWindows()
                ? $"{Name}{suffix}.dll"
                : OperatingSystem.IsMacOS()
                    ? $"lib{Name}{suffix}.dylib"
                    : $"lib{Name}{suffix}.so",

            ProjectType.Interface => "",  // Header-only libraries have no output file

            _ => Name
        };
    }

    /// <summary>
    /// Gets the import library file name for shared libraries on Windows.
    /// </summary>
    /// <param name="config">The build configuration.</param>
    /// <returns>The import library file name, or the output file name for non-shared libraries.</returns>
    public string GetImportLibraryFileName(BuildConfiguration config = BuildConfiguration.Debug)
    {
        if (Type == ProjectType.SharedLibrary && OperatingSystem.IsWindows())
        {
            var suffix = config == BuildConfiguration.Debug ? "_d" : "";
            return $"{Name}{suffix}.lib";
        }
        return GetOutputFileName(config);
    }
}
