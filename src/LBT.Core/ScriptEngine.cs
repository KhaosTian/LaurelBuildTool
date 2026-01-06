using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace LBT;

/// <summary>
/// Roslyn script engine - Loads and executes build.cs scripts.
/// </summary>
public class ScriptEngine
{
    private readonly ScriptOptions _scriptOptions;
    private readonly HashSet<string> _loadedScripts = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ScriptEngine"/> class.
    /// </summary>
    public ScriptEngine()
    {
        // Configure script options
        _scriptOptions = ScriptOptions.Default
            // Reference LBT.Core assembly
            .AddReferences(typeof(Project).Assembly)
            .AddReferences(typeof(Console).Assembly)
            // Import commonly used namespaces
            .AddImports(
                "System",
                "System.IO",
                "System.Collections.Generic",
                "System.Linq",
                "System.Threading.Tasks",
                "LBT"
            )
            // Static imports: allow using SetProject(), Target(), CppStandard.Cpp17, etc. directly
            .AddImports(
                "LBT.BuildSystem",  // SetProject, SetVersion, SetLanguages, AddDefines, Include, etc.
                "LBT.Project"       // Target, Create, CreateStaticLibrary, CreateSharedLibrary
            )
            // Static imports for enums - make constants available without type prefix
            .AddImports(
                "LBT.CppStandard",  // Cpp11, Cpp14, Cpp17, Cpp20, Cpp23
                "LBT.CStandard",    // C99, C11, C17, C23
                "LBT.ProjectType",  // Executable, StaticLibrary, SharedLibrary
                "LBT.Architecture", // X86, X64, Arm, Arm64
                "LBT.Platform",     // Windows, Linux, MacOS, IOS, Android
                "LBT.BuildRule"     // ModeDebug, ModeRelease
            )
            // Allow unsafe code (if users need it)
            .WithAllowUnsafe(true);
    }

    /// <summary>
    /// Loads and executes a script file.
    /// </summary>
    /// <param name="scriptPath">The path to the script file.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task LoadScriptAsync(string scriptPath)
    {
        var fullPath = Path.GetFullPath(scriptPath);

        // Avoid loading the same script multiple times
        if (!_loadedScripts.Add(fullPath))
        {
            return;
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Script file does not exist: {fullPath}");
        }

        var scriptContent = await File.ReadAllTextAsync(fullPath);
        var scriptDir = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;

        // Set current directory to the script's directory
        var originalDir = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = scriptDir;

            // Create and execute script
            var script = CSharpScript.Create(
                scriptContent,
                _scriptOptions,
                globalsType: typeof(ScriptGlobals)
            );

            // Compile and check for errors
            var diagnostics = script.Compile();
            var errors = diagnostics.Where(d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error).ToList();

            if (errors.Any())
            {
                var errorMessages = string.Join(Environment.NewLine,
                    errors.Select(e => $"  {e.Location}: {e.GetMessage()}"));
                throw new InvalidOperationException($"Script compilation error ({scriptPath}):\n{errorMessages}");
            }

            // Execute script
            var globals = new ScriptGlobals
            {
                ScriptDirectory = scriptDir,
                ScriptPath = fullPath
            };

            await script.RunAsync(globals);
        }
        finally
        {
            Environment.CurrentDirectory = originalDir;
        }
    }

    /// <summary>
    /// Resets the engine state.
    /// </summary>
    public void Reset()
    {
        _loadedScripts.Clear();
    }
}

/// <summary>
/// Script global variables.
/// </summary>
public class ScriptGlobals
{
    /// <summary>
    /// The current script's directory.
    /// </summary>
    public string ScriptDirectory { get; init; } = "";

    /// <summary>
    /// The current script file path.
    /// </summary>
    public string ScriptPath { get; init; } = "";
}
