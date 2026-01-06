namespace LBT.Toolchain;

/// <summary>
/// Toolchain type.
/// </summary>
public enum ToolchainType
{
    /// <summary>
    /// Microsoft Visual C++.
    /// </summary>
    MSVC,

    /// <summary>
    /// Clang/LLVM compiler.
    /// </summary>
    Clang,

    /// <summary>
    /// GNU Compiler Collection.
    /// </summary>
    GCC
}

/// <summary>
/// Abstract base class for compilers.
/// </summary>
public abstract class Toolchain
{
    /// <summary>
    /// Toolchain type.
    /// </summary>
    public abstract ToolchainType Type { get; }

    /// <summary>
    /// Toolchain name.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Compiler version.
    /// </summary>
    public string? Version { get; protected set; }

    /// <summary>
    /// Compiler executable path.
    /// </summary>
    public string CompilerPath { get; protected set; } = "";

    /// <summary>
    /// Linker executable path.
    /// </summary>
    public string LinkerPath { get; protected set; } = "";

    /// <summary>
    /// Archiver tool path (for static libraries).
    /// </summary>
    public string ArchiverPath { get; protected set; } = "";

    /// <summary>
    /// Build environment variables (some toolchains like MSVC require specific environment variables).
    /// Defaults to null, indicating the current process environment should be used.
    /// </summary>
    public virtual IReadOnlyDictionary<string, string?>? BuildEnvironment => null;

    /// <summary>
    /// Detects whether the toolchain is available.
    /// </summary>
    /// <returns>True if the toolchain is available; otherwise, false.</returns>
    public abstract Task<bool> DetectAsync();

    /// <summary>
    /// Initializes the build environment (required for some toolchains like MSVC).
    /// Default implementation does nothing.
    /// </summary>
    /// <returns>True if initialization succeeded; otherwise, false.</returns>
    public virtual Task<bool> InitializeEnvironmentAsync()
    {
        return Task.FromResult(true);
    }

    /// <summary>
    /// Gets the compile command line arguments.
    /// </summary>
    /// <param name="options">The compile options.</param>
    /// <returns>The compile command.</returns>
    public abstract CompileCommand GetCompileCommand(CompileOptions options);

    /// <summary>
    /// Gets the link command line arguments.
    /// </summary>
    /// <param name="options">The link options.</param>
    /// <returns>The link command.</returns>
    public abstract LinkCommand GetLinkCommand(LinkOptions options);

    /// <summary>
    /// Auto-detects and returns an available toolchain.
    /// </summary>
    /// <param name="preferred">The preferred toolchain type.</param>
    /// <returns>The detected toolchain, or null if none available.</returns>
    public static async Task<Toolchain?> DetectAsync(ToolchainType? preferred = null)
    {
        var toolchains = new List<Toolchain>();

        if (OperatingSystem.IsWindows())
        {
            toolchains.Add(new MsvcToolchain());
            toolchains.Add(new ClangToolchain());
        }
        else
        {
            toolchains.Add(new ClangToolchain());
            toolchains.Add(new GccToolchain());
        }

        // If a preferred toolchain is specified, try detecting it first
        if (preferred.HasValue)
        {
            var preferredToolchain = toolchains.FirstOrDefault(t => t.Type == preferred.Value);
            if (preferredToolchain != null && await preferredToolchain.DetectAsync())
            {
                return preferredToolchain;
            }
        }

        // Otherwise try in order
        foreach (var toolchain in toolchains)
        {
            if (await toolchain.DetectAsync())
            {
                return toolchain;
            }
        }

        return null;
    }
}

/// <summary>
/// Compile options.
/// </summary>
public class CompileOptions
{
    /// <summary>
    /// Gets or initializes the source file path.
    /// </summary>
    public required string SourceFile { get; init; }

    /// <summary>
    /// Gets or initializes the output file path.
    /// </summary>
    public required string OutputFile { get; init; }

    /// <summary>
    /// Gets or initializes the build configuration.
    /// </summary>
    public BuildConfiguration Configuration { get; init; } = BuildConfiguration.Debug;

    /// <summary>
    /// Gets or initializes the list of include directories.
    /// </summary>
    public List<string> IncludeDirs { get; init; } = new();

    /// <summary>
    /// Gets or initializes the list of preprocessor defines.
    /// </summary>
    public List<string> Defines { get; init; } = new();

    /// <summary>
    /// Gets or initializes the list of extra compiler flags.
    /// </summary>
    public List<string> ExtraFlags { get; init; } = new();

    /// <summary>
    /// Gets or initializes a value indicating whether to generate dependency files.
    /// </summary>
    public bool GenerateDependencies { get; init; } = true;

    /// <summary>
    /// Gets or initializes the dependency file path.
    /// </summary>
    public string? DependencyFile { get; init; }
}

/// <summary>
/// Link options.
/// </summary>
public class LinkOptions
{
    /// <summary>
    /// Gets or initializes the list of object files to link.
    /// </summary>
    public required List<string> ObjectFiles { get; init; }

    /// <summary>
    /// Gets or initializes the output file path.
    /// </summary>
    public required string OutputFile { get; init; }

    /// <summary>
    /// Gets or initializes the output type (executable or library).
    /// </summary>
    public ProjectType OutputType { get; init; } = ProjectType.Executable;

    /// <summary>
    /// Gets or initializes the build configuration.
    /// </summary>
    public BuildConfiguration Configuration { get; init; } = BuildConfiguration.Debug;

    /// <summary>
    /// Gets or initializes the list of libraries to link.
    /// </summary>
    public List<string> Libraries { get; init; } = new();

    /// <summary>
    /// Gets or initializes the list of library search directories.
    /// </summary>
    public List<string> LibraryDirs { get; init; } = new();

    /// <summary>
    /// Gets or initializes the list of extra linker flags.
    /// </summary>
    public List<string> ExtraFlags { get; init; } = new();
}

/// <summary>
/// Compile command.
/// </summary>
public class CompileCommand
{
    /// <summary>
    /// Gets or initializes the compiler executable path.
    /// </summary>
    public required string Executable { get; init; }

    /// <summary>
    /// Gets or initializes the compiler arguments.
    /// </summary>
    public required List<string> Arguments { get; init; }

    /// <summary>
    /// Returns a string representation of the compile command.
    /// </summary>
    /// <returns>The full command line string.</returns>
    public override string ToString() => $"{Executable} {string.Join(" ", Arguments)}";
}

/// <summary>
/// Link command.
/// </summary>
public class LinkCommand
{
    /// <summary>
    /// Gets or initializes the linker executable path.
    /// </summary>
    public required string Executable { get; init; }

    /// <summary>
    /// Gets or initializes the linker arguments.
    /// </summary>
    public required List<string> Arguments { get; init; }

    /// <summary>
    /// Returns a string representation of the link command.
    /// </summary>
    /// <returns>The full command line string.</returns>
    public override string ToString() => $"{Executable} {string.Join(" ", Arguments)}";
}
