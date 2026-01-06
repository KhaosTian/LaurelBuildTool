using Spectre.Console;
using LBT.Cache;
using LBT.Scheduler;
using LBT.Toolchain;

namespace LBT;

/// <summary>
/// Build engine - Core build logic.
/// </summary>
public class BuildEngine
{
    private readonly ScriptEngine _scriptEngine;
    private BuildConfiguration _configuration = BuildConfiguration.Debug;
    private string _command = "build";

    /// <summary>
    /// Initializes a new instance of the <see cref="BuildEngine"/> class.
    /// </summary>
    public BuildEngine()
    {
        _scriptEngine = new ScriptEngine();
    }

    /// <summary>
    /// Executes the build process.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    /// <returns>Exit code (0 for success, non-zero for failure).</returns>
    public async Task<int> ExecuteAsync(string[] args)
    {
        try
        {
            // Parse command line arguments
            ParseArguments(args);

            // Load main build script
            var mainScript = Path.Combine(BuildSystem.RootDirectory, "build.cs");
            if (!File.Exists(mainScript))
            {
                AnsiConsole.MarkupLine($"[red]Error: Cannot find build.cs in {BuildSystem.RootDirectory}[/]");
                return 1;
            }

            await _scriptEngine.LoadScriptAsync(mainScript);

            // Load all included sub-module scripts
            foreach (var includePath in BuildSystem.GetIncludedPaths())
            {
                await _scriptEngine.LoadScriptAsync(includePath);
            }

            // Execute different operations based on command
            return _command switch
            {
                "build" => await BuildProjectsAsync(),
                "clean" => await CleanProjectsAsync(),
                "run" => await BuildAndRunAsync(),
                _ => await BuildProjectsAsync()
            };
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Build failed: {ex.Message.EscapeMarkup()}[/]");
            if (ex.InnerException != null)
            {
                AnsiConsole.MarkupLine($"[red]{ex.InnerException.Message.EscapeMarkup()}[/]");
            }
            return 1;
        }
    }

    private void ParseArguments(string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "build" or "clean" or "run":
                    _command = args[i].ToLower();
                    break;
                case "-c" or "--config":
                    if (i + 1 < args.Length)
                    {
                        _configuration = args[++i].ToLower() switch
                        {
                            "debug" => BuildConfiguration.Debug,
                            "release" => BuildConfiguration.Release,
                            "relwithdebinfo" => BuildConfiguration.RelWithDebInfo,
                            "minsizerel" => BuildConfiguration.MinSizeRel,
                            _ => BuildConfiguration.Debug
                        };
                    }
                    break;
            }
        }
    }

    private async Task<int> BuildProjectsAsync()
    {
        var projects = BuildSystem.Projects.Values.ToList();

        if (projects.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No project definitions found[/]");
            return 0;
        }

        // Detect toolchain
        AnsiConsole.MarkupLine("[blue]Detecting compiler...[/]");
        var toolchain = await Toolchain.Toolchain.DetectAsync();
        if (toolchain == null)
        {
            AnsiConsole.MarkupLine("[red]Error: Cannot find available C++ compiler[/]");
            AnsiConsole.MarkupLine("[yellow]Please ensure Visual Studio, Clang, or GCC is installed[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[green]Using: {toolchain.Name} {toolchain.Version}[/]");

        // Initialize toolchain environment variables (required for MSVC)
        if (!await toolchain.InitializeEnvironmentAsync())
        {
            AnsiConsole.MarkupLine("[yellow]Warning: Cannot initialize build environment, some compilation may fail[/]");
        }

        // Build dependency graph
        var graph = DependencyGraph.Build();
        if (graph.HasCycle(out var cycle))
        {
            AnsiConsole.MarkupLine($"[red]Error: Circular dependency detected: {string.Join(" -> ", cycle!)}[/]");
            return 1;
        }

        var buildOrder = graph.GetBuildOrder();
        AnsiConsole.MarkupLine($"[blue]Build order: {string.Join(" -> ", buildOrder.Select(p => p.Name))}[/]");
        AnsiConsole.WriteLine();

        // Create cache manager
        using var cache = new BuildCacheManager(BuildSystem.RootDirectory);

        // Build each project in order
        foreach (var project in buildOrder)
        {
            AnsiConsole.MarkupLine($"[bold]═══ {project.Name} ═══[/]");
            AnsiConsole.WriteLine();

            // Compile
            var compileScheduler = new CompileScheduler(toolchain, cache, _configuration);
            var compileSuccess = await compileScheduler.CompileProjectAsync(project);

            if (!compileSuccess)
            {
                return 1;
            }

            // Link
            var linkScheduler = new LinkScheduler(toolchain, _configuration);
            var linkSuccess = await linkScheduler.LinkProjectAsync(project);

            if (!linkSuccess)
            {
                return 1;
            }

            AnsiConsole.WriteLine();
        }

        AnsiConsole.MarkupLine("[green bold]Build complete![/]");
        return 0;
    }

    private async Task<int> CleanProjectsAsync()
    {
        AnsiConsole.MarkupLine("[blue]Cleaning build artifacts...[/]");

        // Clean unified build directory
        var buildDir = Path.Combine(BuildSystem.RootDirectory, "build");
        if (Directory.Exists(buildDir))
        {
            Directory.Delete(buildDir, true);
            AnsiConsole.MarkupLine($"[green]Deleted: {buildDir}[/]");
        }

        // Clean legacy output directories (for backward compatibility)
        foreach (var project in BuildSystem.Projects.Values)
        {
            var outputDir = Path.Combine(project.Directory, project.OutputDirectory);
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, true);
                AnsiConsole.MarkupLine($"[green]Deleted: {outputDir}[/]");
            }
        }

        // Clean cache
        var cacheDir = Path.Combine(BuildSystem.RootDirectory, ".lbt", "cache.db");
        if (File.Exists(cacheDir))
        {
            File.Delete(cacheDir);
            AnsiConsole.MarkupLine("[green]Cache cleared[/]");
        }

        await Task.CompletedTask;
        AnsiConsole.MarkupLine("[green bold]Clean complete![/]");
        return 0;
    }

    private async Task<int> BuildAndRunAsync()
    {
        var result = await BuildProjectsAsync();
        if (result != 0)
        {
            return result;
        }

        // Find the first executable project
        var execProject = BuildSystem.Projects.Values
            .FirstOrDefault(p => p.Type == ProjectType.Executable);

        if (execProject == null)
        {
            AnsiConsole.MarkupLine("[yellow]No executable project found[/]");
            return 0;
        }

        var outputDir = Path.Combine(execProject.Directory, execProject.OutputDirectory, _configuration.ToString().ToLower());
        var executable = Path.Combine(outputDir, execProject.GetOutputFileName(_configuration));

        if (!File.Exists(executable))
        {
            AnsiConsole.MarkupLine($"[red]Cannot find executable: {executable}[/]");
            return 1;
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold blue]═══ Running ═══[/]");
        AnsiConsole.WriteLine();

        var psi = new System.Diagnostics.ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = outputDir,
            UseShellExecute = false
        };

        var process = System.Diagnostics.Process.Start(psi);
        if (process != null)
        {
            await process.WaitForExitAsync();
            return process.ExitCode;
        }

        return 1;
    }
}
